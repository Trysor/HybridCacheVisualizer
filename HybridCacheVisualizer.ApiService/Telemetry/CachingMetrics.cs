using HybridCacheVisualizer.Abstractions;
using System.Diagnostics.Metrics;

namespace HybridCacheVisualizer.ApiService.Telemetry;

/// <summary>
/// Provides metrics for monitoring cache hits and misses.
/// </summary>
/// <remarks>This class is designed to track and record metrics for both local and distributed caching systems. It
/// uses counters to measure the number of cache hits and misses, categorized by cache type and strategy.</remarks>
public class CachingMetrics
{
    private readonly Counter<int> _cacheHitCounter;
    private readonly Counter<int> _cacheMissCounter;

    public CachingMetrics(IMeterFactory factory)
    {
        var meter = factory.Create(Constants.Telemetry.ApiService.Sources.MeterName);
        _cacheHitCounter = meter.CreateCounter<int>(Constants.Telemetry.ApiService.MetricNames.CacheHitCounter, description: Constants.Telemetry.ApiService.MetricDescriptions.CacheHitCounter);
        _cacheMissCounter = meter.CreateCounter<int>(Constants.Telemetry.ApiService.MetricNames.CacheMissCounter, description: Constants.Telemetry.ApiService.MetricDescriptions.CacheMissCounter);
    }

    public void RecordMemoryCacheHit(string cacheStrategy) => _cacheHitCounter.Add(1,
        new(Constants.Telemetry.ApiService.TagNames.CacheStrategy, cacheStrategy),
        new(Constants.Telemetry.ApiService.TagNames.CacheType, Constants.Telemetry.ApiService.CacheTypeTags.Local));
    public void RecordMemoryCacheMiss(string cacheStrategy) => _cacheMissCounter.Add(1,
        new(Constants.Telemetry.ApiService.TagNames.CacheStrategy, cacheStrategy),
        new(Constants.Telemetry.ApiService.TagNames.CacheType, Constants.Telemetry.ApiService.CacheTypeTags.Local));

    public void RecordDistributedCacheHit(string cacheStrategy) => _cacheHitCounter.Add(1,
        new(Constants.Telemetry.ApiService.TagNames.CacheStrategy, cacheStrategy),
        new(Constants.Telemetry.ApiService.TagNames.CacheType, Constants.Telemetry.ApiService.CacheTypeTags.Distributed));
    public void RecordDistributedCacheMiss(string cacheStrategy) => _cacheMissCounter.Add(1,
        new(Constants.Telemetry.ApiService.TagNames.CacheStrategy, cacheStrategy),
        new(Constants.Telemetry.ApiService.TagNames.CacheType, Constants.Telemetry.ApiService.CacheTypeTags.Distributed));
}
