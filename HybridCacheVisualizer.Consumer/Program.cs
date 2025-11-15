using HybridCacheVisualizer.Abstractions;
using HybridCacheVisualizer.Abstractions.DataObjects;
using HybridCacheVisualizer.Abstractions.Dtos;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();

builder.Services.AddValidation();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddHttpClient(name: Constants.ServiceNames.ApiService, client =>
{
    // This URL uses "https+http://" to indicate HTTPS is preferred over HTTP.
    // Learn more about service discovery scheme resolution at https://aka.ms/dotnet/sdschemes.
    client.BaseAddress = new($"https+http://{Constants.ServiceNames.ApiService}");
    client.Timeout = TimeSpan.FromSeconds(30);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi("/v1/swagger.json");
    app.UseSwaggerUI(x =>
    {
        x.RoutePrefix = string.Empty;
    });
}

var group = app.MapGroup(Constants.Endpoints.Consumer.StampedeGroup)
    .WithSummary("Simulate cache stampede with specified caching strategy")
    .WithDescription("Performs concurrent requests using the specified caching strategy.");

group.MapPost(Constants.Endpoints.Consumer.Stampede.Raw, (StampedeRequest request, IHttpClientFactory factory, ILogger<Program> logger)
    => PerformStampedeAsync(Constants.Cache.Strategies.Raw, request, factory, logger)).WithName("Raw");

group.MapPost(Constants.Endpoints.Consumer.Stampede.HybridCache, (StampedeRequest request, IHttpClientFactory factory, ILogger<Program> logger)
    => PerformStampedeAsync(Constants.Cache.Strategies.HybridCache, request, factory, logger)).WithName("HybridCache");

group.MapPost(Constants.Endpoints.Consumer.Stampede.Protected, (StampedeRequest request, IHttpClientFactory factory, ILogger<Program> logger)
    => PerformStampedeAsync(Constants.Cache.Strategies.Protected, request, factory, logger)).WithName("Protected");

group.MapPost(Constants.Endpoints.Consumer.Stampede.Unprotected, (StampedeRequest request, IHttpClientFactory factory, ILogger<Program> logger)
    => PerformStampedeAsync(Constants.Cache.Strategies.Unprotected, request, factory, logger)).WithName("Unprotected");

app.MapDefaultEndpoints();

app.Run();


static async Task<IResult> PerformStampedeAsync(string strategy, StampedeRequest request, IHttpClientFactory factory, ILogger<Program> logger)
{
    LogStampede(logger, request);

    string endpoint = $"movies/{request.MovieId}/{strategy}";
    var client = factory.CreateClient(Constants.ServiceNames.ApiService);

    // Create concurrent tasks for stampede simulation
    var tasks = new List<Task<Movie?>>(request.Count);
    for (int i = 0; i < request.Count; i++)
        tasks.Add(client.GetFromJsonAsync<Movie>(endpoint));

    try
    {
        var movies = await Task.WhenAll(tasks).ConfigureAwait(false);

        // Check if all movies were retrieved successfully
        return movies.All(movie => movie != null)
            ? Results.Ok()
            : TypedResults.Problem("Some movies were not retrieved successfully.");
    }
    catch (Exception ex)
    {
        return TypedResults.Problem($"Error during stampede execution: {ex.Message}");
    }
}

public partial class Program
{
    [LoggerMessage(
    Level = LogLevel.Information,
    Message = "Executing Stampede")]
    private static partial void LogStampede(ILogger<Program> logger, [LogProperties] StampedeRequest data);
}
