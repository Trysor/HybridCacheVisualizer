using Abstractions;
using HybridCacheVisualizer.ApiService.Telemetry;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using StackExchange.Redis;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;

namespace HybridCacheVisualizer.ApiService;

public class CacheService
{
    private readonly IMemoryCache _memoryCache;
    private readonly IDistributedCache _distributedCache;
    private readonly IConnectionMultiplexer? _redisMultiplexer;

    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();
    private static readonly SemaphoreSlim _globalLock = new(1, 1);

    private static readonly MemoryCacheEntryOptions MemoryCacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = CacheConstants.MemoryCacheAbsoluteExpiration
    };
    private static readonly DistributedCacheEntryOptions DistributedCacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = CacheConstants.DistributedCacheAbsoluteExpiration
    };

    public CacheService(IMemoryCache cache, IDistributedCache distributedCache, IConnectionMultiplexer? redisMultiplexer = null)
    {
        _memoryCache = cache;
        _distributedCache = distributedCache;

        // hard coupling to Redis here.
        _redisMultiplexer = redisMultiplexer;
    }

    public void SetCacheValue(string key, Movie value)
    {
        // Set value in memory cache
        _memoryCache.Set(key, value, MemoryCacheOptions);
        // Set value in distributed cache
        var serialized = JsonSerializer.Serialize(value);
        _distributedCache.SetString(key, serialized, DistributedCacheOptions);
    }

    // Get value from cache asynchronously without stampede protection
    public async Task<Movie?> GetCacheValueAsync(string key, Func<Task<Movie?>> valueFactory)
    {
        using var activity = ApiServiceTelemetry.StartActivity(ApiServiceTelemetry.OperationNames.GetCacheValueAsync);
        activity?.AddTag(ApiServiceTelemetry.TagNames.CacheKey, key);
        activity?.AddTag(ApiServiceTelemetry.TagNames.MemoryCacheMiss, false);
        activity?.AddTag(ApiServiceTelemetry.TagNames.DistributedCacheMiss, false);

        if (_memoryCache.TryGetValue(key, out Movie? value))
        {
            activity?.AddEvent(new ActivityEvent("Retrieved from memory cache"));
            return value;
        }

        activity?.SetTag(ApiServiceTelemetry.TagNames.MemoryCacheMiss, true);

        var serialized = _distributedCache.GetString(key);
        if (serialized != null)
        {
            activity?.AddEvent(new ActivityEvent("Retrieved from distributed cache"));
            value = JsonSerializer.Deserialize<Movie>(serialized);
            if (value != null)
            {
                activity?.AddEvent(new ActivityEvent("Updating memory cache"));
                _memoryCache.Set(key, value, MemoryCacheOptions);
                return value;
            }
        }

        activity?.SetTag(ApiServiceTelemetry.TagNames.DistributedCacheMiss, true);

        // Value not in cache, call external source to fetch it
        activity?.AddEvent(new ActivityEvent("Retrieving value from external source"));
        value = await valueFactory();
        if (value != null)
        {
            activity?.AddEvent(new ActivityEvent("Updating cache values"));
            SetCacheValue(key, value);
        }
        return value;
    }

    // Get value from cache with stampede protection
    public async Task<Movie?> GetCacheValueWithStampedeProtectionAsync(string key, Func<Task<Movie?>> valueFactory)
    {
        using var activity = ApiServiceTelemetry.StartActivity(ApiServiceTelemetry.OperationNames.GetCacheValueWithStampedeProtectionAsync);
        activity?.AddTag(ApiServiceTelemetry.TagNames.CacheKey, key);
        activity?.AddTag(ApiServiceTelemetry.TagNames.MemoryCacheMiss, false);
        activity?.AddTag(ApiServiceTelemetry.TagNames.DistributedCacheMiss, false);

        // Wait for global lock if purge is in progress
        activity?.AddEvent(new ActivityEvent("Waiting for global lock"));
        await _globalLock.WaitAsync();
        try
        {
            if (_memoryCache.TryGetValue(key, out Movie? value))
            {
                activity?.AddEvent(new ActivityEvent("Retrieved from memory cache"));
                return value;
            }

            activity?.SetTag(ApiServiceTelemetry.TagNames.MemoryCacheMiss, true);

            var serialized = _distributedCache.GetString(key);
            if (serialized != null)
            {
                activity?.AddEvent(new ActivityEvent("Retrieved from distributed cache"));
                value = JsonSerializer.Deserialize<Movie>(serialized);
                if (value != null)
                {
                    activity?.AddEvent(new ActivityEvent("Updating memory cache"));
                    _memoryCache.Set(key, value, MemoryCacheOptions);
                    return value;
                }
            }

            activity?.SetTag(ApiServiceTelemetry.TagNames.DistributedCacheMiss, true);

            activity?.AddEvent(new ActivityEvent("Acquiring lock"));
            var myLock = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
            await myLock.WaitAsync();
            try
            {
                // Double-check after acquiring lock
                if (_memoryCache.TryGetValue(key, out value))
                {
                    activity?.AddEvent(new ActivityEvent("Retrieved from memory cache AFTER aquiring lock"));
                    return value;
                }
                serialized = _distributedCache.GetString(key);
                if (serialized != null)
                {
                    activity?.AddEvent(new ActivityEvent("Retrieved from distributed cache AFTER aquiring lock"));
                    value = JsonSerializer.Deserialize<Movie>(serialized);
                    if (value != null)
                    {
                        _memoryCache.Set(key, value, MemoryCacheOptions);
                        return value;
                    }
                }

                // Value not in cache, call external source to fetch it
                activity?.AddEvent(new ActivityEvent("Retrieving value from external source"));
                value = await valueFactory();
                if (value != null)
                {
                    activity?.AddEvent(new ActivityEvent("Updating cache values"));
                    SetCacheValue(key, value);
                }
                return value;
            }
            finally
            {
                activity?.AddEvent(new ActivityEvent("Releasing lock"));
                myLock.Release();
                // Optionally clean up lock
                if (_locks.TryGetValue(key, out var semaphore) && semaphore.CurrentCount == 1)
                {
                    _locks.TryRemove(key, out _);
                }
            }
        }
        finally
        {
            activity?.AddEvent(new ActivityEvent("Releasing global lock"));
            _globalLock.Release();
        }
    }

    // Flush both memory and distributed cache
    public async Task FlushCacheAsync()
    {
        using var activity = ApiServiceTelemetry.StartActivity(ApiServiceTelemetry.OperationNames.FlushCacheAsync);

        // Lock down the cache during purge
        await _globalLock.WaitAsync();
        try
        {
            // Wait for all other locks to be released
            foreach (var kvp in _locks)
            {
                await kvp.Value.WaitAsync();
            }

            try
            {
                // Purge memory cache: IMemoryCache does not provide a direct clear method, so we need to cast to MemoryCache
                if (_memoryCache is MemoryCache memCache)
                {
                    activity?.AddEvent(new ActivityEvent("Clearing memory cache"));
                    memCache.Clear(); // Clear all entries
                }

                // Purge distributed cache: IDistributedCache does not provide a direct clear method
                // If using Redis, flush the database (for demo/dev only, not for production!)
                if (_distributedCache is Microsoft.Extensions.Caching.StackExchangeRedis.RedisCache && _redisMultiplexer is not null)
                {
                    // Demo purpose: show how unfeasible this is.

                    activity?.AddEvent(new ActivityEvent("Attempting to flush Redis Cache"));

                    foreach (var server in _redisMultiplexer.GetServers())
                    {
                        activity?.AddEvent(new ActivityEvent($"Flushing Redis server"));
                        // StackExchange.Redis.RedisCommandException:
                        // "This operation is not available unless admin mode is enabled: FLUSHALL"
                        await server.FlushAllDatabasesAsync();
                        // this would clear all databases on the Redis server, regardless of
                        // the key area used. Say we had Books and Movies in the same Redis instance,
                        // this would clear both, which is not great.
                    }
                }
            }
            finally
            {
                // Release all other locks
                foreach (var kvp in _locks)
                {
                    kvp.Value.Release();
                }
            }
        }
        finally
        {
            // Clear all locks at the end
            _locks.Clear();
            _globalLock.Release();
        }
    }
}
