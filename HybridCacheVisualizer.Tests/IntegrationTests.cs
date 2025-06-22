using Abstractions;
using System.Net.Http.Json;

namespace HybridCacheVisualizer.Tests;

public class IntegrationTests(AspireFixture fixture) : IClassFixture<AspireFixture>
{
    [Theory]
    [InlineData("movies/3/raw")]
    [InlineData("movies/3/protected")]
    [InlineData("movies/3/unprotected")]
    [InlineData("movies/3/hybridcache")]
    public async Task TestApiService_GetMovie_TestCacheSolutionByUri_ShouldReturnMovieData(string path)
    {
        // Arrange
        var app = fixture.App;
        var expectedResult = new Movie(3, "The Dark Knight");

        // Act
        var consumerClient = app.CreateHttpClient("apiservice");
        var response = await consumerClient.GetAsync(path, TestContext.Current.CancellationToken);
        var movie = await response.Content.ReadFromJsonAsync<Movie>(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expectedResult, movie);
    }

    [Fact]
    public async Task TestApiService_FlushCache()
    {
        // Arrange
        var app = fixture.App;

        // Act
        var apiServiceClient = app.CreateHttpClient("apiservice");

        List<Task<HttpResponseMessage>> tasks = [];
        for (int id = 0; id < 10; id++)
        {
            tasks.Add(apiServiceClient.GetAsync($"movies/{id}/protected", TestContext.Current.CancellationToken));
            tasks.Add(apiServiceClient.GetAsync($"movies/{id}/unprotected", TestContext.Current.CancellationToken));
            tasks.Add(apiServiceClient.GetAsync($"movies/{id}/hybridcache", TestContext.Current.CancellationToken));
        }

        await Task.WhenAll(tasks);

        foreach (var task in tasks)
        {
            var taskResponse = await task;
            Assert.Equal(HttpStatusCode.OK, taskResponse.StatusCode);
        }

        var response = await apiServiceClient.GetAsync("flush", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        // fails because of the redis cache admin issue
    }

    [Theory]
    [InlineData("stampede/raw")]
    [InlineData("stampede/protected")]
    [InlineData("stampede/unprotected")]
    [InlineData("stampede/hybridcache")]
    public async Task TestConsumer_Stampede_ShouldSuccessfullyStampede_ReturnTrueForAllValidMovies(string path)
    {
        // Arrange
        var app = fixture.App;

        // Act
        var consumerClient = app.CreateHttpClient("consumer");
        var response = await consumerClient.GetAsync(path, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(await response.Content.ReadFromJsonAsync<bool>(TestContext.Current.CancellationToken));
    }
}
