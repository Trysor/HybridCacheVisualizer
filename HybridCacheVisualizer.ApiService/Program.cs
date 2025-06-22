using Abstractions;
using HybridCacheVisualizer.ApiService;
using HybridCacheVisualizer.ApiService.Telemetry;
using Microsoft.Extensions.Caching.Hybrid;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Listen to our own ActivitySources
builder.Services.ConfigureOpenTelemetryTracerProvider(builder =>
{
    builder.AddSource(ApiServiceTelemetry.ActivitySourceName);
});

// Add services to the container.
builder.Services.AddProblemDetails();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Configure Aspire client integrations
builder.AddSqlServerClient("movies-database"); // same name as the call to AddDatabase in the AppHost
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

// Add the database service
builder.Services.AddScoped<DatabaseService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

var moviesGroup = app.MapGroup("/movies").WithName("Movies");

moviesGroup.MapGet("{id}/raw", GetMovieByIdRaw).WithName("Raw");
moviesGroup.MapGet("{id}/protected", GetMovieByIdProtected).WithName("Protected");
moviesGroup.MapGet("{id}/unprotected", GetMovieByIdUnprotected).WithName("Unprotected");
moviesGroup.MapGet("{id}/hybridcache", GetMovieByIdHybridCache).WithName("HybridCache");

app.MapGet("flush", async (HybridCache hybrid, CacheService cacheService, CancellationToken cancel) =>
{
    await hybrid.RemoveByTagAsync("movies", cancel);
    await cacheService.FlushCacheAsync();
})
.WithName("Flush");

app.MapDefaultEndpoints();

app.Run();


static async Task<IResult> GetMovieByIdRaw(int id, DatabaseService db, CancellationToken cancel)
    => await db.QueryForMovieByIdAsync(id, cancel)
    is Movie movie
        ? TypedResults.Ok(movie)
        : TypedResults.NotFound();

static async Task<IResult> GetMovieByIdProtected(int id, CacheService cacheService, DatabaseService db, CancellationToken cancel)
    => await cacheService.GetCacheValueWithStampedeProtectionAsync($"movies:{id}:protected",
            async () => await db.QueryForMovieByIdAsync(id, cancel))
    is Movie movie
        ? TypedResults.Ok(movie)
        : TypedResults.NotFound();

static async Task<IResult> GetMovieByIdUnprotected(int id, CacheService cacheService, DatabaseService db, CancellationToken cancel)
    => await cacheService.GetCacheValueAsync($"movies:{id}:unprotected",
            async () => await db.QueryForMovieByIdAsync(id, cancel))
    is Movie movie
        ? TypedResults.Ok(movie)
        : TypedResults.NotFound();

static async Task<IResult> GetMovieByIdHybridCache(int id, HybridCache hybridCache, DatabaseService db, CancellationToken cancel)
{
    var key = $"movies:{id}:hybridcache";
    var state = new HybridCacheState(db, key, id);

    return await hybridCache.GetOrCreateAsync(key, state,
        static async (state, cancel) => await state.DatabaseService.QueryForMovieByIdAsync(state.RecordId, cancel),
        tags: ["movies"]
        //cancellationToken: cancel
    ) is Movie movie
        ? TypedResults.Ok(movie)
        : TypedResults.NotFound();
}

readonly record struct HybridCacheState(DatabaseService DatabaseService, string Key, int RecordId);