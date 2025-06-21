using System.Net.Http.Json;

namespace HybridCacheVisualizer.Tests;

public class IntegrationTests(AspireFixture fixture) : IClassFixture<AspireFixture>
{

    [Theory]
    [InlineData("/stampedeOldUnprotected")]
    [InlineData("/stampedeOldWithStampedeProt")]
    [InlineData("/stampedeSql")]
    [InlineData("/stampedeHybridCache")]
    public async Task TestStampede_ShouldReturnTrue_IndicateAllMoviesReturnedValue(string path)
    {
        // Arrange
        var app = fixture.App;

        // Act
        var httpClient = app.CreateHttpClient("consumer");
        var response = await httpClient.GetAsync(path, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(await response.Content.ReadFromJsonAsync<bool>(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TestFlushCache()
    {
        // Arrange
        var app = fixture.App;

        List<string> movies = [
            "The Shawshank Redemption",
            "The Godfather",
            "The Dark Knight",
            "Pulp Fiction",
            "Forrest Gump",
            "Inception",
            "Fight Club",
            "The Matrix",
            "Goodfellas",
            "The Lord of the Rings: The Return of the King",
        ];


        // Act
        var httpClient = app.CreateHttpClient("apiservice");

        List<Task<HttpResponseMessage>> tasks = [];
        foreach (var movie in movies)
        {
            tasks.Add(httpClient.GetAsync($"oldcacheunprotected/movies/{movie}", TestContext.Current.CancellationToken));
            tasks.Add(httpClient.GetAsync($"oldcache/movies/{movie}", TestContext.Current.CancellationToken));
            tasks.Add(httpClient.GetAsync($"hybridcache/movies/{movie}", TestContext.Current.CancellationToken));
        }

        await Task.WhenAll(tasks);

        foreach (var task in tasks)
        {
            var taskResponse = await task;
            Assert.Equal(HttpStatusCode.OK, taskResponse.StatusCode);
        }

        var response = await httpClient.GetAsync("/flush", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
