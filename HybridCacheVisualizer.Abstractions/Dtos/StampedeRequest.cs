using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace HybridCacheVisualizer.Abstractions.Dtos;

/// <summary>
/// Request parameters for stampede endpoints with comprehensive OpenAPI validation and documentation.
/// </summary>
/// <param name="MovieId">The movie ID to request. Must be between 1 and 10 inclusive.</param>
/// <param name="Count">Number of concurrent requests to make. Must be between 1 and 100 inclusive.</param>
public record StampedeRequest(
    [Required, Range(1, 10, ErrorMessage = "MovieId must be between 1 and 10.")]
    [Description("The movie ID to request. Valid movie IDs are 1-10, representing different movies in the test database.")]
    int MovieId = 3,

    [Required, Range(1, 100, ErrorMessage = "Count must be between 1 and 100.")]
    [Description("Number of concurrent requests to simulate the cache stampede scenario.")]
    int Count = 20
);