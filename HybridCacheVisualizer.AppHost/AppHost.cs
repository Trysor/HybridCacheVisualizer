using HybridCacheVisualizer.Abstractions;
using HybridCacheVisualizer.Abstractions.Dtos;
using System.Diagnostics;
using System.Net.Http.Json;

var builder = DistributedApplication.CreateBuilder(args);

var redisCache = builder.AddRedis(Constants.ServiceNames.RedisCache);

var sqlServer = builder.AddSqlServer(Constants.ServiceNames.SqlServer).WithLifetime(ContainerLifetime.Persistent);

var databaseName = Constants.ServiceNames.MoviesDatabase;
var creationScript = $$"""
    IF DB_ID('{{databaseName}}') IS NULL
        CREATE DATABASE [{{databaseName}}];
    GO

    -- Use the database
    USE [{{databaseName}}];
    GO

    CREATE TABLE movies (
        id INT PRIMARY KEY IDENTITY(1,1),
        title VARCHAR(255) NOT NULL UNIQUE
    );
    GO

    INSERT INTO movies (title) VALUES
        ('The Shawshank Redemption'),
        ('The Godfather'),
        ('The Dark Knight'),
        ('Pulp Fiction'),
        ('Forrest Gump'),
        ('Inception'),
        ('Fight Club'),
        ('The Matrix'),
        ('Goodfellas'),
        ('The Lord of the Rings: The Return of the King');
    GO
    """;

var aspireDb = sqlServer.AddDatabase(databaseName).WithCreationScript(creationScript);

var apiService = builder.AddProject<Projects.HybridCacheVisualizer_ApiService>(Constants.ServiceNames.ApiService)
    .WithHttpHealthCheck("/health")
    .WithReference(redisCache)
    .WaitFor(redisCache)
    .WithReference(aspireDb)
    .WaitFor(aspireDb)
    .WithHttpCommand(
        path: $"/{Constants.Endpoints.ApiService.Flush}",
        displayName: Constants.AspireDashboard.Actions.FlushCache,
        commandOptions: CreateHttpCommandOptions("Delete", HttpMethod.Get)
    );

builder.AddProject<Projects.HybridCacheVisualizer_Consumer>(Constants.ServiceNames.Consumer)
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WaitFor(apiService)
    .WithHttpCommand(
        path: $"/{Constants.Endpoints.Consumer.StampedeGroup}{Constants.Endpoints.Consumer.Stampede.HybridCache}",
        displayName: Constants.AspireDashboard.Actions.StampedeHybridCache,
        commandOptions: CreateHttpCommandOptions("ThumbLike")
    )
    .WithHttpCommand(
        path: $"/{Constants.Endpoints.Consumer.StampedeGroup}{Constants.Endpoints.Consumer.Stampede.Raw}",
        displayName: Constants.AspireDashboard.Actions.StampedeSql,
        commandOptions: CreateHttpCommandOptions("Database")
    )
    .WithHttpCommand(
        path: $"/{Constants.Endpoints.Consumer.StampedeGroup}{Constants.Endpoints.Consumer.Stampede.Protected}",
        displayName: Constants.AspireDashboard.Actions.StampedeCacheProtected,
        commandOptions: CreateHttpCommandOptions("FastForward")
    )
    .WithHttpCommand(
        path: $"/{Constants.Endpoints.Consumer.StampedeGroup}{Constants.Endpoints.Consumer.Stampede.Unprotected}",
        displayName: Constants.AspireDashboard.Actions.StampedeCacheUnprotected,
        commandOptions: CreateHttpCommandOptions("DatabaseWarning")
    );


builder.Build().Run();

static HttpCommandOptions CreateHttpCommandOptions(string icon, HttpMethod? method = null)
{
    return new HttpCommandOptions()
    {
        Method = method ?? HttpMethod.Post,
        IconName = icon,
        IconVariant = IconVariant.Filled,
        IsHighlighted = true,
        PrepareRequest = (request) =>
        {
            request.Request.Content = JsonContent.Create(new StampedeRequest(MovieId: 3, Count: 20), SerializationContext.Default.StampedeRequest);

            // Clear any existing activity. We want to see our actions independently in Aspire.
            Activity.Current = null;
            return Task.CompletedTask;
        }
    };
}
