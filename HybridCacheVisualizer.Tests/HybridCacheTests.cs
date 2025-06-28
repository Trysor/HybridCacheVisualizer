using HybridCacheVisualizer.Abstractions;
using HybridCacheVisualizer.Abstractions.DataObjects;
using HybridCacheVisualizer.Tests.Helpers;
using Microsoft.Extensions.Caching.Hybrid;

namespace HybridCacheVisualizer.Tests;

/// <summary>
/// Test suite demonstrating HybridCache functionality and behavior.
/// These tests showcase key features like caching efficiency, null value handling,
/// custom serialization, and immutable vs mutable object caching strategies.
/// </summary>
public class HybridCacheTests
{
    /// <summary>
    /// Verifies that HybridCache efficiently caches values by calling the backing source only once,
    /// even when multiple requests are made for the same key.
    /// </summary>
    [Fact]
    public async Task GetOrCreateAsync_WhenSameKeyAccessedMultipleTimes_ShouldCallBackingSourceOnlyOnce()
    {
        var callCount = 0;

        // Arrange
        HybridCache sut = HybridCacheBuilder.Create().Build();
        string key = Constants.Cache.Keys.CreateMovieKey(1, Constants.Cache.Strategies.HybridCache);

        ValueTask<Movie?> CallBackingSource(Movie value, CancellationToken cancel)
        {
            Interlocked.Increment(ref callCount);
            return ValueTask.FromResult<Movie?>(value);
        }

        // Act - First call retrieves from backing source and caches the result
        var result1 = await sut.GetOrCreateAsync(
            key,
            new Movie(1, "Inception"),
            CallBackingSource,
            cancellationToken: TestContext.Current.CancellationToken);

        // Act - Second call should retrieve from cache, not backing source
        var result2 = await sut.GetOrCreateAsync(
            key,
            new Movie(1, "Different Title"), // This value should be ignored (cached value used)
            CallBackingSource,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert - Demonstrate caching efficiency
        Assert.Equal(1, callCount); // Backing source called only once
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.Equal("Inception", result1.Title); // Original cached value
        Assert.Equal("Inception", result2.Title); // Same cached value returned
        Assert.Equal(result1, result2); // Same object reference
    }

    /// <summary>
    /// Verifies that HybridCache efficiently caches null values, preventing redundant calls 
    /// to the backing source when the same non-existent item is requested multiple times.
    /// </summary>
    [Fact]
    public async Task GetOrCreateAsync_WhenBackingSourceReturnsNull_ShouldCacheNullValues()
    {
        var callCount = 0;

        // Arrange
        HybridCache sut = HybridCacheBuilder.Create().Build();
        string key = Constants.Cache.Keys.CreateMovieKey(1, Constants.Cache.Strategies.HybridCache);

        ValueTask<Movie?> CallBackingSource(CancellationToken cancel)
        {
            Interlocked.Increment(ref callCount);
            return ValueTask.FromResult<Movie?>(null); // Simulate non-existent item
        }

        // Act - First call returns null and caches it
        var result1 = await sut.GetOrCreateAsync(
            key,
            CallBackingSource,
            cancellationToken: TestContext.Current.CancellationToken);

        // Act - Second call should return cached null without calling backing source
        var result2 = await sut.GetOrCreateAsync(
            key,
            CallBackingSource,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert - Null values are cached efficiently (negative caching)
        Assert.Equal(1, callCount); // Backing source called only once
        Assert.Null(result1);
        Assert.Null(result2);
    }

    /// <summary>
    /// Verifies that HybridCache can utilize custom serializers for specialized 
    /// serialization and deserialization of cached objects.
    /// </summary>
    [Fact]
    public async Task GetOrCreateAsync_WhenCustomSerializerProvided_ShouldUseSpecializedSerializer()
    {
        // Arrange - Create a custom serializer to track usage
        SimpleUtfMovieSerializer customSerializer = new();

        HybridCache sut = HybridCacheBuilder.Create()
            .AddSerializer(customSerializer)
            .Build();

        string key = Constants.Cache.Keys.CreateMovieKey(1, Constants.Cache.Strategies.HybridCache);
        Movie movie = new(1, "Inception");

        // Act - Use HybridCache with custom serializer
        var result1 = await sut.GetOrCreateAsync<Movie?>(
            key,
            (cancel) => new(movie),
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert - Custom serializer was used
        Assert.NotNull(result1);
        Assert.Equal(movie, result1);
        Assert.Equal(1, customSerializer.SerializeCallCount);
        Assert.Equal(1, customSerializer.DeserializeCallCount);
    }

    /// <summary>
    /// Verifies that HybridCache returns different object instances when caching mutable objects,
    /// preventing cache pollution that could occur if the same object instance was shared.
    /// </summary>
    [Fact]
    public async Task GetOrCreateAsync_WhenCachingMutableObjects_ShouldReturnDifferentInstances()
    {
        var callCount = 0;

        // Arrange
        HybridCache sut = HybridCacheBuilder.Create().Build();
        string key = Constants.Cache.Keys.CreateMovieKey(2, Constants.Cache.Strategies.HybridCache);
        var movie = new Movie(2, "The Matrix");

        ValueTask<Movie?> CallBackingSource(Movie value, CancellationToken cancel)
        {
            Interlocked.Increment(ref callCount);
            return ValueTask.FromResult<Movie?>(new(value.Id, value.Title)); // Always return new instance
        }

        // Act - Get the same cached item twice
        var result1 = await sut.GetOrCreateAsync(
            key,
            movie,
            CallBackingSource,
            cancellationToken: TestContext.Current.CancellationToken);

        var result2 = await sut.GetOrCreateAsync(
            key,
            movie,
            CallBackingSource,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert - Different instances prevent mutation issues
        Assert.Equal(1, callCount); // Backing source called only once
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.Equal("The Matrix", result1.Title);
        Assert.Equal("The Matrix", result2.Title);
        Assert.NotSame(result1, result2); // Different object instances for safety
    }

    /// <summary>
    /// Verifies that HybridCache returns the same object instance when caching immutable objects,
    /// optimizing memory usage through safe object reuse.
    /// </summary>
    [Fact]
    public async Task GetOrCreateAsync_WhenCachingImmutableObjects_ShouldReturnSameInstance()
    {
        var callCount = 0;

        // Arrange
        HybridCache sut = HybridCacheBuilder.Create().Build();
        string key = Constants.Cache.Keys.CreateMovieKey(2, Constants.Cache.Strategies.HybridCache);
        var movie = new ImmutableMovie(2, "The Matrix");

        ValueTask<ImmutableMovie?> CallBackingSource(ImmutableMovie value, CancellationToken cancel)
        {
            Interlocked.Increment(ref callCount);
            return ValueTask.FromResult<ImmutableMovie?>(new(value.Id, value.Title)); // Always return new instance
        }

        // Act - Get the same cached item twice
        var result1 = await sut.GetOrCreateAsync(
            key,
            movie,
            CallBackingSource,
            cancellationToken: TestContext.Current.CancellationToken);

        var result2 = await sut.GetOrCreateAsync(
            key,
            movie,
            CallBackingSource,
            cancellationToken: TestContext.Current.CancellationToken);

        // Assert - Same instance is safe for immutable objects and saves memory
        Assert.Equal(1, callCount); // Backing source called only once
        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.Equal("The Matrix", result1.Title);
        Assert.Equal("The Matrix", result2.Title);
        Assert.Same(result1, result2); // Same object instance is safe for immutable types
    }
}
