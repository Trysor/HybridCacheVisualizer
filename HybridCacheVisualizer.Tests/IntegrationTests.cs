using HybridCacheVisualizer.Abstractions;
using HybridCacheVisualizer.Abstractions.DataObjects;
using HybridCacheVisualizer.Abstractions.Dtos;
using HybridCacheVisualizer.Tests.Helpers;
using System.Net.Http.Json;

namespace HybridCacheVisualizer.Tests;

/// <summary>
/// Integration tests demonstrating end-to-end functionality across multiple services.
/// These tests validate the complete system including API endpoints, caching strategies,
/// and distributed service communication using Aspire for orchestration.
/// </summary>
public class IntegrationTests(AspireFixture fixture) : IClassFixture<AspireFixture>
{
    /// <summary>
    /// Verifies that all movie API endpoints (raw, protected, unprotected, and hybridcache) 
    /// return the correct movie data with successful HTTP status codes.
    /// </summary>
    [Theory]
    [InlineData($"{Constants.Endpoints.ApiService.MoviesGroup}/3/{Constants.Cache.Strategies.Raw}")]
    [InlineData($"{Constants.Endpoints.ApiService.MoviesGroup}/3/{Constants.Cache.Strategies.Protected}")]
    [InlineData($"{Constants.Endpoints.ApiService.MoviesGroup}/3/{Constants.Cache.Strategies.Unprotected}")]
    [InlineData($"{Constants.Endpoints.ApiService.MoviesGroup}/3/{Constants.Cache.Strategies.HybridCache}")]
    public async Task GetMovieEndpoints_WhenRequestingValidMovieId_ShouldReturnCorrectMovieData(string path)
    {
        // Arrange
        var app = fixture.App;
        var expectedResult = new Movie(3, "The Dark Knight");

        // Act - Call different caching strategy endpoints
        var apiServiceClient = app.CreateHttpClient(Constants.ServiceNames.ApiService);
        var response = await apiServiceClient.GetAsync(path, TestContext.Current.CancellationToken);
        var movie = await response.Content.ReadFromJsonAsync<Movie>(TestContext.Current.CancellationToken);

        // Assert - All strategies should return the same data
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expectedResult, movie);
    }

    /// <summary>
    /// Verifies that the cache flush endpoint properly handles cache clearing operations,
    /// including the expected failure due to Redis cache administration limitations.
    /// </summary>
    [Fact]
    public async Task FlushCacheEndpoint_WhenCacheIsPopulatedAndFlushed_ShouldReturnExpectedErrorStatus()
    {
        // Arrange
        var app = fixture.App;
        var apiServiceClient = app.CreateHttpClient(Constants.ServiceNames.ApiService);

        // Act - Populate cache with multiple requests across different strategies
        List<Task<HttpResponseMessage>> cachingTasks = [];
        for (int id = 1; id <= 10; id++)
        {
            cachingTasks.Add(apiServiceClient.GetAsync($"{Constants.Endpoints.ApiService.MoviesGroup}/{id}/{Constants.Cache.Strategies.Protected}", TestContext.Current.CancellationToken));
            cachingTasks.Add(apiServiceClient.GetAsync($"{Constants.Endpoints.ApiService.MoviesGroup}/{id}/{Constants.Cache.Strategies.Unprotected}", TestContext.Current.CancellationToken));
            cachingTasks.Add(apiServiceClient.GetAsync($"{Constants.Endpoints.ApiService.MoviesGroup}/{id}/{Constants.Cache.Strategies.HybridCache}", TestContext.Current.CancellationToken));
        }

        await Task.WhenAll(cachingTasks);

        // Verify all caching requests succeeded
        foreach (var task in cachingTasks)
        {
            var taskResponse = await task;
            Assert.Equal(HttpStatusCode.OK, taskResponse.StatusCode);
        }

        // Act - Attempt to flush cache (demonstrates Redis admin limitation)
        var response = await apiServiceClient.GetAsync(Constants.Endpoints.ApiService.Flush, TestContext.Current.CancellationToken);

        // Assert - Expected failure due to Redis configuration
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        // Note: Fails because Redis admin mode is required for FLUSHALL command
    }

    /// <summary>
    /// Verifies that the consumer service can successfully execute cache stampede scenarios
    /// using different caching strategies.
    /// </summary>
    [Theory]
    [InlineData(Constants.Cache.Strategies.Raw)]
    [InlineData(Constants.Cache.Strategies.Protected)]
    [InlineData(Constants.Cache.Strategies.Unprotected)]
    [InlineData(Constants.Cache.Strategies.HybridCache)]
    public async Task StampedeEndpoint_WhenExecutingCacheStampedeWithValidStrategy_ShouldReturnSuccessStatus(string strategy)
    {
        // Arrange
        var app = fixture.App;

        // Act - Execute cache stampede simulation
        var consumerClient = app.CreateHttpClient(Constants.ServiceNames.Consumer);

        StampedeRequest request = new()
        {
            MovieId = 3, // Using a valid movie ID for testing
            Count = 20 // Simulate 20 concurrent requests
        };

        var response = await consumerClient.PostAsJsonAsync($"{Constants.Endpoints.Consumer.StampedeGroup}/{strategy}", request, TestContext.Current.CancellationToken);

        // Assert - All strategies should handle concurrent load successfully
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
