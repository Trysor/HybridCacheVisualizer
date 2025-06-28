using HybridCacheVisualizer.Abstractions;
using HybridCacheVisualizer.ApiService;
using HybridCacheVisualizer.ApiService.Telemetry;
using Microsoft.Extensions.Caching.Hybrid;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenTelemetry()
    .WithTracing(builder =>
{
    builder.AddSource(Constants.Telemetry.ApiService.Sources.ActivitySourceName);
})
    .WithMetrics(builder =>
{
    builder.AddMeter(Constants.Telemetry.ApiService.Sources.MeterName);
});

builder.AddServiceDefaults();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

builder.AddSqlServerClient(Constants.ServiceNames.MoviesDatabase);
builder.AddRedisDistributedCache(Constants.ServiceNames.RedisCache);

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<CacheService>();
builder.Services.AddSingleton<CachingMetrics>();

builder.Services.AddHybridCache(options =>
{
    options.DefaultEntryOptions = new()
    {
        Expiration = Constants.Cache.Configuration.DistributedCacheExpirationTime,
        LocalCacheExpiration = Constants.Cache.Configuration.MemoryCacheExpirationTime,
    };
});

builder.Services.AddScoped<DatabaseService>();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi("/v1/swagger.json");
    app.UseSwaggerUI(x =>
    {
        x.RoutePrefix = string.Empty;
    });
}

var moviesGroup = app.MapGroup(Constants.Endpoints.ApiService.MoviesGroup).WithName("Movies");
moviesGroup.MapGet(Constants.Endpoints.ApiService.Movies.Raw, GetMovieByIdRaw).WithName("Raw");
moviesGroup.MapGet(Constants.Endpoints.ApiService.Movies.Protected, GetMovieByIdProtected).WithName("Protected");
moviesGroup.MapGet(Constants.Endpoints.ApiService.Movies.Unprotected, GetMovieByIdUnprotected).WithName("Unprotected");
moviesGroup.MapGet(Constants.Endpoints.ApiService.Movies.HybridCache, GetMovieByIdHybridCache).WithName("HybridCache");

app.MapGet(Constants.Endpoints.ApiService.Flush, ExecuteFlush).WithName("Flush");
app.MapDefaultEndpoints();
app.Run();

static async Task<IResult> GetMovieByIdRaw(int id, DatabaseService db, CancellationToken cancel)
    => ReturnOkOrNotFound(await db.QueryForMovieByIdAsync(id, cancel).ConfigureAwait(false));

static async Task<IResult> GetMovieByIdProtected(int id, CacheService cacheService, DatabaseService db, CancellationToken cancel)
    => ReturnOkOrNotFound(
        await cacheService.GetCacheValueWithStampedeProtectionAsync(
            Constants.Cache.Keys.CreateMovieKey(id, Constants.Cache.Strategies.Protected),
            async () => await db.QueryForMovieByIdAsync(id, cancel).ConfigureAwait(false)).ConfigureAwait(false));

static async Task<IResult> GetMovieByIdUnprotected(int id, CacheService cacheService, DatabaseService db, CancellationToken cancel)
    => ReturnOkOrNotFound(
        await cacheService.GetCacheValueAsync(
            Constants.Cache.Keys.CreateMovieKey(id, Constants.Cache.Strategies.Unprotected),
            async () => await db.QueryForMovieByIdAsync(id, cancel).ConfigureAwait(false)).ConfigureAwait(false));

static async Task<IResult> GetMovieByIdHybridCache(int id, HybridCache hybridCache, DatabaseService db, CancellationToken cancel)
    => ReturnOkOrNotFound(
        await hybridCache.GetOrCreateAsync(
            Constants.Cache.Keys.CreateMovieKey(id, Constants.Cache.Strategies.HybridCache),
            new HybridCacheState(db, id),
            static async (state, cancel) => await state.DatabaseService.QueryForMovieByIdAsync(state.RecordId, cancel).ConfigureAwait(false),
            //cancellationToken: cancel,
            tags: [Constants.Cache.Tags.Movies]).ConfigureAwait(false));

static async Task<IResult> ExecuteFlush(HybridCache hybridCache, CacheService cacheService, CancellationToken cancel)
{
    await hybridCache.RemoveByTagAsync(Constants.Cache.Tags.Movies, cancel).ConfigureAwait(false);
    await cacheService.FlushCacheAsync().ConfigureAwait(false);
    return TypedResults.Ok("Cache flushed successfully.");
}

static IResult ReturnOkOrNotFound<T>(T? value) where T : class => value is null ? TypedResults.NotFound() : TypedResults.Ok(value);

readonly record struct HybridCacheState(DatabaseService DatabaseService, int RecordId);