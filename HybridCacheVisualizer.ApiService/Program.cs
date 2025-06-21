using Abstractions;
using HybridCacheVisualizer.ApiService;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Hybrid;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Configure Aspire client integrations
builder.AddSqlServerClient("app-db"); // same name as the call to AddDatabase in the AppHost
builder.AddRedisDistributedCache("redis-cache");

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<CacheService>(); // old system

// new system
builder.Services.AddHybridCache(options =>
{
    options.DefaultEntryOptions = new()
    {
        Expiration = CacheConstants.DistributedCacheAbsoluteExpiration,
        LocalCacheExpiration = CacheConstants.MemoryCacheAbsoluteExpiration
    };
});


var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();


if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/movies/{title}", async (SqlConnection connection, string title) =>
{
    return await DatabaseService.QueryDatabase(connection, title);
})
.WithName("GetMovie");


app.MapGet("oldcache/movies/{title}", async (CacheService cacheService, SqlConnection connection, string title) =>
{
    return await cacheService.GetCacheValueWithStampedeProtectionAsync($"stampedeprotected:movie:{title}",
        async () => await DatabaseService.QueryDatabase(connection, title)
    );
})
.WithName("OldCacheGetMovie");

app.MapGet("oldcacheunprotected/movies/{title}", async (CacheService cacheService, SqlConnection connection, string title) =>
{
    return await cacheService.GetCacheValueAsync($"unprotected:movie:{title}",
        async () => await DatabaseService.QueryDatabase(connection, title)
    );
})
.WithName("OldCacheUnprotectedGetMovie");

app.MapGet("hybridcache/movies/{title}", async (HybridCache hybridCache, SqlConnection connection, string title) =>
{
    var key = $"hybridcache:movie:{title}";
    var state = new HybridCacheState(connection, key, title);

    return await hybridCache.GetOrCreateAsync(key, state,
        static async (state, cancel) => await DatabaseService.QueryDatabase(state.Connection, state.Title),
        tags: ["movies"]
    );
})
.WithName("HybridCacheGetMovie");


app.MapGet("flush", async (HybridCache hybrid, CacheService cacheService, CancellationToken cancel) =>
{
    await hybrid.RemoveByTagAsync("movies", cancel);
    await cacheService.FlushCacheAsync();
});

app.MapDefaultEndpoints();

app.Run();


public record struct HybridCacheState(SqlConnection Connection, string Key, string Title);