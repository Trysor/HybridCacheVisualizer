namespace HybridCacheVisualizer.Abstractions;

/// <summary>
/// Centralized constants organized by domain and service context.
/// This ensures consistency across the application and eliminates magic strings.
/// </summary>
public static class Constants
{
    /// <summary>
    /// Service names for Aspire orchestration
    /// </summary>
    public static class ServiceNames
    {
        public const string ApiService = "apiservice";
        public const string Consumer = "consumer";
        public const string RedisCache = "redis-cache";
        public const string SqlServer = "sqlserver";
        public const string MoviesDatabase = "movies-database";
    }

    /// <summary>
    /// Cache-related constants including strategies, configuration, keys, and tags
    /// </summary>
    public static class Cache
    {
        /// <summary>
        /// Cache strategy names used throughout the application
        /// </summary>
        public static class Strategies
        {
            public const string Raw = "raw";
            public const string Protected = "protected";
            public const string Unprotected = "unprotected";
            public const string HybridCache = "hybridcache";
        }

        /// <summary>
        /// Cache configuration settings
        /// </summary>
        public static class Configuration
        {
            /// <summary>
            /// Memory cache expiration time (L1 cache)
            /// </summary>
            public static readonly TimeSpan MemoryCacheExpirationTime = TimeSpan.FromSeconds(10);

            /// <summary>
            /// Distributed cache expiration time (L2 cache)
            /// </summary>
            public static readonly TimeSpan DistributedCacheExpirationTime = TimeSpan.FromSeconds(30);
        }

        /// <summary>
        /// Cache key prefixes and patterns
        /// </summary>
        public static class Keys
        {
            public const string MoviesPrefix = "movies";

            /// <summary>
            /// Creates a cache key for a movie with the specified strategy
            /// </summary>
            /// <param name="movieId">The movie ID</param>
            /// <param name="strategy">The caching strategy</param>
            /// <returns>A formatted cache key</returns>
            public static string CreateMovieKey(int movieId, string strategy) => $"{MoviesPrefix}:{movieId}:{strategy}";
        }

        /// <summary>
        /// Cache tags for HybridCache invalidation
        /// </summary>
        public static class Tags
        {
            public const string Movies = "movies";
            public const string All = "*";
        }
    }

    /// <summary>
    /// Endpoints organized by service context
    /// </summary>
    public static class Endpoints
    {
        /// <summary>
        /// API Service endpoints for movie operations
        /// </summary>
        public static class ApiService
        {
            public const string MoviesGroup = "/movies";
            public const string Flush = "flush";

            /// <summary>
            /// Movie endpoint patterns (use with string interpolation)
            /// </summary>
            public static class Movies
            {
                public const string Raw = "{id}/raw";
                public const string Protected = "{id}/protected";
                public const string Unprotected = "{id}/unprotected";
                public const string HybridCache = "{id}/hybridcache";
            }
        }

        /// <summary>
        /// Consumer Service endpoints for stampede simulation
        /// </summary>
        public static class Consumer
        {
            public const string StampedeGroup = "stampede";

            /// <summary>
            /// Stampede endpoint patterns for Consumer service
            /// </summary>
            public static class Stampede
            {
                public const string Raw = "/raw";
                public const string Protected = "/protected";
                public const string Unprotected = "/unprotected";
                public const string HybridCache = "/hybridcache";
            }
        }
    }

    /// <summary>
    /// Telemetry and metrics constants organized by service area
    /// </summary>
    public static class Telemetry
    {
        /// <summary>
        /// API Service telemetry constants including activity sources, meters, operations, and metrics
        /// </summary>
        public static class ApiService
        {
            /// <summary>
            /// Activity source and meter names for API service telemetry infrastructure
            /// </summary>
            public static class Sources
            {
                /// <summary>
                /// Activity source name for API service tracing
                /// </summary>
                public const string ActivitySourceName = "HybridCacheVisualizer.ApiService";

                /// <summary>
                /// Meter name for API service metrics
                /// </summary>
                public const string MeterName = "HybridCacheVisualizer.ApiService.CachingMetrics";
            }

            /// <summary>
            /// Cache type values for telemetry tags
            /// </summary>
            public static class CacheTypeTags
            {
                public const string Local = "Local";
                public const string Distributed = "Distributed";
            }

            /// <summary>
            /// Tag names for telemetry
            /// </summary>
            public static class TagNames
            {
                public const string CacheType = "CacheType";
                public const string CacheStrategy = "CacheStrategy";
                public const string CacheKey = "CacheKey";
                public const string MemoryCacheMiss = "MemoryCacheMiss";
                public const string DistributedCacheMiss = "DistributedCacheMiss";
            }

            /// <summary>
            /// Meter and counter names
            /// </summary>
            public static class MetricNames
            {
                public const string CacheHitCounter = "CacheHitCounter";
                public const string CacheMissCounter = "CacheMissCounter";
            }

            /// <summary>
            /// Metric descriptions for telemetry
            /// </summary>
            public static class MetricDescriptions
            {
                public const string CacheHitCounter = "Counts the number of cache hits.";
                public const string CacheMissCounter = "Counts the number of cache misses.";
            }
        }
    }

    public static class AspireDashboard
    {
        /// <summary>
        /// Display names for UI Actions in the Aspire dashboard
        /// </summary>
        public static class Actions
        {
            public const string FlushCache = "Flush Cache";
            public const string StampedeHybridCache = "Stampede HybridCache";
            public const string StampedeSql = "Stampede SQL";
            public const string StampedeCacheProtected = "Stampede Cache [Prot.]";
            public const string StampedeCacheUnprotected = "Stampede Cache [Unprot.]";
        }
    }
}