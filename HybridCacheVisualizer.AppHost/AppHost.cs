using System.Diagnostics;

var builder = DistributedApplication.CreateBuilder(args);

var redisCache = builder.AddRedis("redis-cache");

var sqlServer = builder.AddSqlServer("sqlserver").WithLifetime(ContainerLifetime.Persistent);

var databaseName = "movies-database";
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

var apiService = builder.AddProject<Projects.HybridCacheVisualizer_ApiService>("apiservice")
    .WithHttpHealthCheck("/health")
    .WithReference(redisCache)
    .WaitFor(redisCache)
    .WithReference(aspireDb)
    .WaitFor(aspireDb)
    .WithHttpCommand(
        path: "/flush",
        displayName: "Flush Cache",
        commandOptions: CreateHttpCommandOptions("Delete")
    );

builder.AddProject<Projects.HybridCacheVisualizer_Consumer>("consumer")
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WaitFor(apiService)
    .WithHttpCommand(
        path: "/stampede/hybridcache",
        displayName: "Stampede HybridCache",
        commandOptions: CreateHttpCommandOptions("ThumbLike")
    )
    .WithHttpCommand(
        path: "/stampede/raw",
        displayName: "Stampede SQL",
        commandOptions: CreateHttpCommandOptions("Database")
    )
    .WithHttpCommand(
        path: "/stampede/protected",
        displayName: "Stampede Cache [Prot.]",
        commandOptions: CreateHttpCommandOptions("FastForward")
    )
    .WithHttpCommand(
        path: "/stampede/unprotected",
        displayName: "Stampede Cache [Unprot.]",
        commandOptions: CreateHttpCommandOptions("DatabaseWarning")
    );


builder.Build().Run();

static HttpCommandOptions CreateHttpCommandOptions(string icon)
{
    return new HttpCommandOptions()
    {
        Method = HttpMethod.Get,
        IconName = icon,
        IconVariant = IconVariant.Filled,
        IsHighlighted = true,
        PrepareRequest = (request) =>
        {
            // Clear any existing activity. We want to see our actions independently in Aspire.
            Activity.Current = null;
            return Task.CompletedTask;
        }
    };
}