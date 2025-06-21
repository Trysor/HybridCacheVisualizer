using Abstractions;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;
using System.Text.Json;

namespace HybridCacheVisualizer.ApiService;

public class CacheService
{
    private readonly IMemoryCache _memoryCache;
    private readonly IDistributedCache _distributedCache;
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

    public CacheService(IMemoryCache cache, IDistributedCache distributedCache)
    {
        _memoryCache = cache;
        _distributedCache = distributedCache;
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
        if (_memoryCache.TryGetValue(key, out Movie? value))
        {
            return value;
        }
        var serialized = _distributedCache.GetString(key);
        if (serialized != null)
        {
            value = JsonSerializer.Deserialize<Movie>(serialized);
            if (value != null)
            {
                _memoryCache.Set(key, value, MemoryCacheOptions);
                return value;
            }
        }
        // Value not in cache, generate it
        value = await valueFactory();
        if (value != null)
        {
            SetCacheValue(key, value);
        }
        return value;
    }

    // Get value from cache with stampede protection
    public async Task<Movie?> GetCacheValueWithStampedeProtectionAsync(string key, Func<Task<Movie?>> valueFactory)
    {
        // Wait for global lock if purge is in progress
        await _globalLock.WaitAsync();
        try
        {
            if (_memoryCache.TryGetValue(key, out Movie? value))
            {
                return value;
            }
            var serialized = _distributedCache.GetString(key);
            if (serialized != null)
            {
                value = JsonSerializer.Deserialize<Movie>(serialized);
                if (value != null)
                {
                    _memoryCache.Set(key, value, MemoryCacheOptions);
                    return value;
                }
            }

            var myLock = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
            await myLock.WaitAsync();
            try
            {
                // Double-check after acquiring lock
                if (_memoryCache.TryGetValue(key, out value))
                {
                    return value;
                }
                serialized = _distributedCache.GetString(key);
                if (serialized != null)
                {
                    value = JsonSerializer.Deserialize<Movie>(serialized);
                    if (value != null)
                    {
                        _memoryCache.Set(key, value, MemoryCacheOptions);
                        return value;
                    }
                }
                // Value not in cache, generate it
                value = await valueFactory();
                if (value != null)
                {
                    SetCacheValue(key, value);
                }
                return value;
            }
            finally
            {
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
            _globalLock.Release();
        }
    }

    // Flush both memory and distributed cache
    public async Task FlushCacheAsync()
    {
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
                    memCache.Clear(); // Clear all entries
                }

                // Purge distributed cache: IDistributedCache does not provide a direct clear method
                // If using Redis, flush the database (for demo/dev only, not for production!)
                if (_distributedCache is Microsoft.Extensions.Caching.StackExchangeRedis.RedisCache redisCache)
                {
                    var field = typeof(Microsoft.Extensions.Caching.StackExchangeRedis.RedisCache)
                        .GetField("_connection", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (field?.GetValue(redisCache) is StackExchange.Redis.ConnectionMultiplexer connection)
                    {
                        // does this even work?
                        var endpoints = connection.GetEndPoints();
                        foreach (var endpoint in endpoints)
                        {
                            var server = connection.GetServer(endpoint);
                            await server.FlushDatabaseAsync();
                        }
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
