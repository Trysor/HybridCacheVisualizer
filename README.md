# HybridCacheVisualizer

## Introduction

HybridCacheVisualizer demonstrates how the new HybridCache in .NET can modernize and simplify caching in distributed applications, replacing the traditional manual implementations using IMemoryCache and IDistributedCache. By showcasing real-world scenarios and performance comparisons, this project highlights why HybridCache is the preferred approach for robust, scalable, and efficient caching in modern .NET solutions.

## About HybridCache

HybridCache is a new caching abstraction in .NET that combines the speed of in-memory caching with the scalability of distributed caching. It provides built-in cache stampede protection, tag-based invalidation, and a unified API, making it a superior alternative to using IMemoryCache and IDistributedCache separately.

**Cache stampede** occurs when multiple concurrent requests for the same uncached data overwhelm the backing store (database). HybridCache automatically prevents this by ensuring only one request fetches the data while others wait for the result.

- [HybridCache Documentation](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/hybrid?view=aspnetcore-9.0)

## Key Learning Outcomes

Upon exploring this project, you will gain deeper insights into the following concepts:

- **Cache Stampede Protection**: How HybridCache prevents database overload during high-concurrency scenarios
- **Unified Caching API**: Simplifying L1 (memory) + L2 (distributed) cache patterns into a single implementation
- **Performance Comparison**: Observable differences between traditional caching vs. HybridCache through telemetry
- **Production Patterns**: Tag-based invalidation, custom serialization, and mutable vs. immutable object caching

### Learning Through Tests

The `HybridCacheVisualizer.Tests` project demonstrates HybridCache features through practical scenarios:

- **Stampede Protection**: Verify backing sources are called only once for repeated requests
- **Null Value Caching**: Understand how HybridCache handles null values  
- **Custom Serialization**: Implement specialized serializers for specific data types
- **Object Mutability**: Compare behavior with mutable vs. immutable cached objects
- **Integration Testing**: End-to-end validation across distributed services

## Prerequisites

- **.NET 9 SDK**: Required to build and run the solution. [Download .NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- **Podman**: Used for container orchestration in development, providing a lightweight alternative to Docker. [Learn more about Podman](https://podman.io/)
- (Optional) **Visual Studio 2022+** or **VS Code** for development.

> **Note:** You can switch from Podman to Docker by removing the `DOTNET_ASPIRE_CONTAINER_RUNTIME` property entirely in [`HybridCacheVisualizer.AppHost/appsettings.Development.json`](HybridCacheVisualizer.AppHost/appsettings.Development.json).

## What is Aspire?

[Aspire](https://learn.microsoft.com/en-us/dotnet/aspire/overview/) is a .NET application model for building, running, and managing cloud-native distributed applications. Aspire simplifies service discovery, health checks, resilience, and telemetry, and—most importantly for this project—automatically orchestrates all dependencies (SQL Server with pre-populated movie data, Redis cache, etc.).

### Observing Performance with Telemetry

Aspire provides built-in OpenTelemetry integration that automatically collects traces, logs, and metrics. When you run the application, the Aspire dashboard provides real-time observability to compare caching strategies:

- **Traces**: View request flows and identify cache hits/misses
- **Metrics**: Monitor cache performance, hit ratios, and response times  
- **Logs**: Debug cache behavior and stampede scenarios
- **Custom Events**: Track cache operations through custom telemetry in the codebase

Use the dashboard to observe how different strategies handle concurrent load and cache efficiency.

### Port Configuration

If you encounter port conflicts, modify `applicationUrl` in Properties/launchSettings.json or configure custom ports using `.WithHttpEndpoint(port: 5001)` in AppHost.cs.

## Running the Project

The default entry point is the `HybridCacheVisualizer.AppHost` project. When you run this project, Aspire will automatically start and orchestrate all required services and dependencies, including the API service, consumer, SQL Server, and Redis.

To run the solution: `dotnet run --project HybridCacheVisualizer.AppHost` 

This will launch the full environment using Aspire and Podman, with all dependencies ready for use.

## Project Structure

- **HybridCacheVisualizer.AppHost**: Orchestrates the distributed application using Aspire, including infrastructure setup (SQL Server, Redis) and service wiring.
- **HybridCacheVisualizer.ApiService**: Exposes endpoints to fetch movie data using different caching strategies (IMemoryCache, IDistributedCache, HybridCache).
- **HybridCacheVisualizer.Consumer**: Simulates load and cache stampede scenarios by stampeding the API endpoints.
- **HybridCacheVisualizer.Abstractions**: Shared models and cache configuration constants.
- **HybridCacheVisualizer.ServiceDefaults**: Provides common service configuration for health checks, telemetry, and service discovery.
- **HybridCacheVisualizer.Tests**: Integration tests for validating cache behavior and system health.

## API Endpoints

### ApiService

- `GET /movies/{id}/raw`: Fetch movie directly from the database by ID.
- `GET /movies/{id}/protected`: Fetch movie using IMemoryCache and IDistributedCache with stampede protection.
- `GET /movies/{id}/unprotected`: Fetch movie using IMemoryCache and IDistributedCache without stampede protection.
- `GET /movies/{id}/hybridcache`: Fetch movie using HybridCache.
- `GET /flush`: Flush all cache entries (memory and distributed).

### Consumer

- `POST /stampede/raw`: Simulate stampede with direct database access (no caching)
- `POST /stampede/protected`: Simulate stampede with traditional cache and stampede protection  
- `POST /stampede/unprotected`: Simulate stampede with traditional cache without stampede protection
- `POST /stampede/hybridcache`: Simulate stampede using HybridCache

All endpoints accept a `StampedeRequest` body to configure the simulation parameters.

