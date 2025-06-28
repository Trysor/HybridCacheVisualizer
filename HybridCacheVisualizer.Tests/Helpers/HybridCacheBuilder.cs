using Microsoft.Extensions.Caching.Hybrid;
using System.Diagnostics.CodeAnalysis;

namespace HybridCacheVisualizer.Tests.Helpers;

internal class HybridCacheBuilder
{
    private readonly ServiceCollection _sc = new();
    private readonly IHybridCacheBuilder _hybridCacheBuilder;

    private bool _isBuilt;

    private HybridCacheBuilder()
    {
        _hybridCacheBuilder = _sc.AddHybridCache(); // only memory cache
    }


    private static void ThrowIfBuilt([DoesNotReturnIf(true)] bool isBuilt)
    {
        if (isBuilt)
            throw new InvalidOperationException("Builder has already been built.");
    }

    public static HybridCacheBuilder Create() => new();

    public HybridCacheBuilder AddSerializer<T>(IHybridCacheSerializer<T> serializer)
    {
        ThrowIfBuilt(_isBuilt);

        _hybridCacheBuilder.AddSerializer(serializer);

        return this;
    }

    public HybridCache Build()
    {
        ThrowIfBuilt(_isBuilt);

        _isBuilt = true;
        return _sc.BuildServiceProvider().GetRequiredService<HybridCache>();
    }
}
