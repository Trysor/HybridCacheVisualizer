using HybridCacheVisualizer.Abstractions;
using HybridCacheVisualizer.ApiService.Telemetry;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using StackExchange.Redis;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;

namespace HybridCacheVisualizer.ApiService;

/// <summary>
/// Provides a multi-layer caching service that supports in-memory and distributed caching,  with optional integration
/// for Redis. This service includes features such as cache stampede  protection, asynchronous cache retrieval, and
/// cache flushing.
/// </summary>
/// <remarks>
/// <para>
/// <b>Limitations:</b> This cache service is inherently limited to handling objects only. The underlying
/// <see cref="IDistributedCache"/> interface provides only <c>Get</c> (which returns a <c>byte[]</c>) and <c>GetString</c> (which returns a <c>string</c>),
/// with no built-in mechanism to track or infer which keys should be deserialized as an object, string or byte-array. To simplify operations this service only deals with objects.
/// </para>
/// </remarks>
public class CacheService
{
    private readonly IMemoryCache _memoryCache;
    private readonly IDistributedCache _distributedCache;
    private readonly IConnectionMultiplexer? _redisMultiplexer;
    private readonly CachingMetrics _metrics;

    private static readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.Ordinal);
    private static readonly SemaphoreSlim _globalLock = new(1, 1);

    private static readonly MemoryCacheEntryOptions MemoryCacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = Constants.Cache.Configuration.MemoryCacheExpirationTime,
    };
    private static readonly DistributedCacheEntryOptions DistributedCacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = Constants.Cache.Configuration.DistributedCacheExpirationTime,
    };

    public CacheService(IMemoryCache cache, IDistributedCache distributedCache, CachingMetrics metrics, IConnectionMultiplexer? redisMultiplexer = null)
    {
        _memoryCache = cache;
        _distributedCache = distributedCache;
        _metrics = metrics;

        // hard coupling to Redis here.
        _redisMultiplexer = redisMultiplexer;
    }

    /// <summary>
    /// Retrieves a cached value associated with the specified key, or generates and caches the value using the provided
    /// factory function if it is not found.
    /// </summary>
    /// <remarks>This method first attempts to retrieve the value from an in-memory cache. If the value is not
    /// found, it checks a distributed cache. If the value is still not found, the <paramref name="valueFactory"/>
    /// function is invoked to generate the value, which is then cached in both memory and distributed caches.</remarks>
    /// <typeparam name="T">The type of the cached value. Must be a reference type.</typeparam>
    /// <param name="key">The key used to identify the cached value. Cannot be null or empty.</param>
    /// <param name="valueFactory">A function that generates the value to cache if it is not found in memory or distributed cache. The function is
    /// invoked only if the value is missing from both caches.</param>
    /// <returns>The cached value of type <typeparamref name="T"/>, or <see langword="null"/> if the value is not found and the
    /// factory function returns <see langword="null"/>.</returns>
    public async Task<T?> GetCacheValueAsync<T>(string key, Func<Task<T?>> valueFactory) where T : class
    {
        using var activity = ApiServiceTelemetry.StartActivity(nameof(GetCacheValueAsync));
        activity?
            .SetTag(Constants.Telemetry.ApiService.TagNames.CacheKey, key)
            .SetTag(Constants.Telemetry.ApiService.TagNames.MemoryCacheMiss, false)
            .SetTag(Constants.Telemetry.ApiService.TagNames.DistributedCacheMiss, false);

        // Check memory cache first
        if (_memoryCache.TryGetValue(key, out T? value))
        {
            activity?.AddEvent(new ActivityEvent("Retrieved from memory cache"));
            _metrics.RecordMemoryCacheHit(Constants.Cache.Strategies.Unprotected);
            return value;
        }

        activity?.SetTag(Constants.Telemetry.ApiService.TagNames.MemoryCacheMiss, true);
        _metrics.RecordMemoryCacheMiss(Constants.Cache.Strategies.Unprotected);

        // Check distributed cache
        var serialized = await _distributedCache.GetStringAsync(key).ConfigureAwait(false);
        if (serialized != null)
        {
            activity?.AddEvent(new ActivityEvent("Retrieved from distributed cache"));
            _metrics.RecordDistributedCacheHit(Constants.Cache.Strategies.Unprotected);

            value = JsonSerializer.Deserialize<T>(serialized);
            if (value != null)
            {
                activity?.AddEvent(new ActivityEvent("Updating memory cache"));
                _memoryCache.Set(key, value, MemoryCacheOptions);
                return value;
            }
        }

        activity?.SetTag(Constants.Telemetry.ApiService.TagNames.DistributedCacheMiss, true);
        _metrics.RecordDistributedCacheMiss(Constants.Cache.Strategies.Unprotected);

        // Value not in cache, call external source to fetch it
        activity?.AddEvent(new ActivityEvent("Retrieving value from external source"));
        value = await valueFactory().ConfigureAwait(false);
        if (value != null)
        {
            activity?.AddEvent(new ActivityEvent("Updating cache values"));
            await SetCacheValueAsync(key, value).ConfigureAwait(false);
        }
        return value;
    }

    /// <summary>
    /// Retrieves a cached value associated with the specified key, using stampede protection to prevent multiple
    /// concurrent calls from overwhelming the cache or external data source.
    /// </summary>
    /// <remarks>This method employs a multi-layer caching strategy, first checking an in-memory cache, then a
    /// distributed cache, and finally invoking the provided <paramref name="valueFactory"/> to fetch the value if it is
    /// not cached. Stampede protection is implemented using locks to ensure that only one caller fetches and updates
    /// the cache when the value is missing.  The method is thread-safe and ensures consistent cache updates across
    /// multiple callers.</remarks>
    /// <typeparam name="T">The type of the cached value. Must be a reference type.</typeparam>
    /// <param name="key">The unique key identifying the cached value. Cannot be null or empty.</param>
    /// <param name="valueFactory">A function that asynchronously generates the value to cache if it is not already present. This function is only
    /// invoked if the value is missing from both memory and distributed caches.</param>
    /// <returns>The cached value of type <typeparamref name="T"/>, or <see langword="null"/> if the value is not found and the
    /// <paramref name="valueFactory"/> does not produce a value.</returns>
    public async Task<T?> GetCacheValueWithStampedeProtectionAsync<T>(string key, Func<Task<T?>> valueFactory) where T : class
    {
        using var activity = ApiServiceTelemetry.StartActivity(nameof(GetCacheValueWithStampedeProtectionAsync));
        activity?
            .SetTag(Constants.Telemetry.ApiService.TagNames.CacheKey, key)
            .SetTag(Constants.Telemetry.ApiService.TagNames.MemoryCacheMiss, false)
            .SetTag(Constants.Telemetry.ApiService.TagNames.DistributedCacheMiss, false);

        // Wait for global lock if purge is in progress
        activity?.AddEvent(new ActivityEvent("Waiting for global lock"));
        await _globalLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_memoryCache.TryGetValue(key, out T? value))
            {
                activity?.AddEvent(new ActivityEvent("Retrieved from memory cache"));
                _metrics.RecordMemoryCacheHit(Constants.Cache.Strategies.Protected);
                return value;
            }

            activity?.SetTag(Constants.Telemetry.ApiService.TagNames.MemoryCacheMiss, true);
            _metrics.RecordMemoryCacheMiss(Constants.Cache.Strategies.Protected);

            var serialized = await _distributedCache.GetStringAsync(key).ConfigureAwait(false);
            if (serialized != null)
            {
                activity?.AddEvent(new ActivityEvent("Retrieved from distributed cache"));
                _metrics.RecordDistributedCacheHit(Constants.Cache.Strategies.Protected);

                value = JsonSerializer.Deserialize<T>(serialized, SerializationContext.Default.Options);
                if (value != null)
                {
                    activity?.AddEvent(new ActivityEvent("Updating memory cache"));
                    _memoryCache.Set(key, value, MemoryCacheOptions);
                    return value;
                }
            }

            activity?.SetTag(Constants.Telemetry.ApiService.TagNames.DistributedCacheMiss, true);
            _metrics.RecordDistributedCacheMiss(Constants.Cache.Strategies.Protected);

            activity?.AddEvent(new ActivityEvent("Acquiring lock"));
            var myLock = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
            await myLock.WaitAsync().ConfigureAwait(false);
            try
            {
                // Double-check after acquiring lock
                if (_memoryCache.TryGetValue(key, out value))
                {
                    activity?.AddEvent(new ActivityEvent("Retrieved from memory cache AFTER aquiring lock"));
                    _metrics.RecordMemoryCacheHit(Constants.Cache.Strategies.Protected);
                    return value;
                }

                activity?.SetTag(Constants.Telemetry.ApiService.TagNames.MemoryCacheMiss, true);
                _metrics.RecordMemoryCacheMiss(Constants.Cache.Strategies.Protected);

                serialized = await _distributedCache.GetStringAsync(key).ConfigureAwait(false);
                if (serialized != null)
                {
                    activity?.AddEvent(new ActivityEvent("Retrieved from distributed cache AFTER aquiring lock"));
                    _metrics.RecordDistributedCacheHit(Constants.Cache.Strategies.Protected);

                    value = JsonSerializer.Deserialize<T>(serialized, SerializationContext.Default.Options);
                    if (value != null)
                    {
                        _memoryCache.Set(key, value, MemoryCacheOptions);
                        return value;
                    }
                }

                // Value not in cache, call external source to fetch it
                activity?.AddEvent(new ActivityEvent("Retrieving value from external source"));
                value = await valueFactory().ConfigureAwait(false);
                if (value != null)
                {
                    activity?.AddEvent(new ActivityEvent("Updating cache values"));
                    await SetCacheValueAsync(key, value).ConfigureAwait(false);
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

    private async Task SetCacheValueAsync<T>(string key, T value) where T : class
    {
        // Set value in memory cache
        _memoryCache.Set(key, value, MemoryCacheOptions);

        var serialized = JsonSerializer.Serialize(value);
        await _distributedCache.SetStringAsync(key, serialized, DistributedCacheOptions).ConfigureAwait(false);
    }


    /// <summary>
    /// Asynchronously flushes all cached data from both memory and distributed caches.
    /// </summary>
    public async Task FlushCacheAsync()
    {
        using var activity = ApiServiceTelemetry.StartActivity(nameof(FlushCacheAsync));

        // Lock down the cache during purge
        await _globalLock.WaitAsync().ConfigureAwait(false);
        try
        {
            // Wait for all other locks to be released
            foreach (var kvp in _locks)
            {
                await kvp.Value.WaitAsync().ConfigureAwait(false);
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
                        await server.FlushAllDatabasesAsync().ConfigureAwait(false);
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
