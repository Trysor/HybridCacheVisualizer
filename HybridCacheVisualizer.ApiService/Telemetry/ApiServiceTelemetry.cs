using HybridCacheVisualizer.Abstractions;
using System.Diagnostics;

namespace HybridCacheVisualizer.ApiService.Telemetry;

/// <summary>
/// Provides telemetry functionality for monitoring and tracing operations within the API service.
/// </summary>
public static class ApiServiceTelemetry
{
    private static readonly ActivitySource _activitySource = new(Constants.Telemetry.ApiService.Sources.ActivitySourceName);

    public static Activity? StartActivity(string operationName) => _activitySource.StartActivity(operationName);
}
