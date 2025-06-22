using System.Diagnostics;

namespace HybridCacheVisualizer.ApiService.Telemetry;

public static class ApiServiceTelemetry
{
    public const string ActivitySourceName = "HybridCacheVisualizer.ApiService";
    private static readonly ActivitySource _activitySource = new(ActivitySourceName);

    public static Activity? StartActivity(string operationName) => _activitySource.StartActivity(operationName);

    public static class OperationNames
    {
        public const string GetCacheValueWithStampedeProtectionAsync = "GetCacheValueWithStampedeProtectionAsync";
        public const string GetCacheValueAsync = "GetCacheValueAsync";
        public const string FlushCacheAsync = "FlushCacheAsync";
    }

    public static class TagNames
    {
        public const string CacheKey = "CacheKey";
        public const string MemoryCacheMiss = "MemoryCacheMiss";
        public const string DistributedCacheMiss = "DistributedCacheMiss";
    }
}
