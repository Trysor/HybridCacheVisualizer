namespace Abstractions;

public static class CacheConstants
{
    public static readonly TimeSpan MemoryCacheAbsoluteExpiration = TimeSpan.FromSeconds(10);
    public static readonly TimeSpan DistributedCacheAbsoluteExpiration = TimeSpan.FromSeconds(30);
}
