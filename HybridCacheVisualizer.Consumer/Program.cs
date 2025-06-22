using Abstractions;
using HybridCacheVisualizer.Consumer;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddProblemDetails();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddHttpClient(name: Constants.HttpClientName, client =>
{
    // This URL uses "https+http://" to indicate HTTPS is preferred over HTTP.
    // Learn more about service discovery scheme resolution at https://aka.ms/dotnet/sdschemes.
    client.BaseAddress = new("https+http://apiservice");
    client.Timeout = TimeSpan.FromSeconds(30);
});


var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

var stampedeGroup = app.MapGroup("stampede").WithName("Stampede");

stampedeGroup.MapGet("raw", async (IHttpClientFactory factory)
    => await PerformStampedeAsync(factory, "/movies/3/raw")
)
.WithName("Raw");

stampedeGroup.MapGet("protected", async (IHttpClientFactory factory)
    => await PerformStampedeAsync(factory, "movies/3/protected")
)
.WithName("Protected");

stampedeGroup.MapGet("unprotected", async (IHttpClientFactory factory)
    => await PerformStampedeAsync(factory, "movies/3/unprotected")
)
.WithName("Unprotected");

stampedeGroup.MapGet("hybridcache", async (IHttpClientFactory factory)
    => await PerformStampedeAsync(factory, "movies/3/hybridcache")
)
.WithName("HybridCache");

app.MapDefaultEndpoints();

app.Run();


static async Task<bool> PerformStampedeAsync(IHttpClientFactory factory, string endpoint)
{
    var client = factory.CreateClient(Constants.HttpClientName);

    int totalRequests = 20;

    List<Task<Movie?>> tasks = [];
    for (int i = 0; i < totalRequests; i++)
        tasks.Add(client.GetFromJsonAsync<Movie>(endpoint));

    var movies = await Task.WhenAll(tasks);

    // check if we find null; if any is found, the cache logic doesn't work fully
    // returns true if none of them are null
    return movies.All(x => x != null);
}