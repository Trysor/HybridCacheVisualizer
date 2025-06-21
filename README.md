# HybridCacheVisualizer

## Introduction

HybridCacheVisualizer demonstrates how the new HybridCache in .NET can modernize and simplify caching in distributed applications, replacing the traditional IMemoryCache and IDistributedCache interfaces. By showcasing real-world scenarios and performance comparisons, this project highlights why HybridCache is the preferred approach for robust, scalable, and efficient caching in modern .NET solutions.

## About HybridCache

HybridCache is a new caching abstraction in .NET that combines the speed of in-memory caching with the scalability of distributed caching. It provides built-in cache stampede protection, tag-based invalidation, and a unified API, making it a superior alternative to using IMemoryCache and IDistributedCache separately.
- [HybridCache Documentation](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/hybrid?view=aspnetcore-9.0)

## Prerequisites

- **.NET 9 SDK**: Required to build and run the solution. [Download .NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- **Podman**: Used for container orchestration in development, providing a lightweight alternative to Docker. [Learn more about Podman](https://podman.io/)
- (Optional) **Visual Studio 2022+** or **VS Code** for development.

> **Note:** You can switch from Podman to Docker by removing the `DOTNET_ASPIRE_CONTAINER_RUNTIME` property entirely in [`HybridCacheVisualizer.AppHost/appsettings.Development.json`](HybridCacheVisualizer.AppHost/appsettings.Development.json).

## What is Aspire?

[Aspire](https://learn.microsoft.com/en-us/dotnet/aspire/overview/) is a .NET application model for building, running, and managing cloud-native distributed applications. Aspire simplifies service discovery, health checks, resilience, and telemetry, and—most importantly for this project—automatically orchestrates all dependencies (such as SQL Server and Redis) and services, so you can focus on development and testing without manual setup.

## Running the Project

The default entry point is the `HybridCacheVisualizer.AppHost` project. When you run this project, Aspire will automatically start and orchestrate all required services and dependencies, including the API service, consumer, SQL Server, and Redis.

To run the solution: `dotnet run --project HybridCacheVisualizer.AppHost` This will launch the full environment using Aspire and Podman, with all dependencies ready for use.

## Project Structure

- **HybridCacheVisualizer.AppHost**: Orchestrates the distributed application using Aspire, including infrastructure setup (SQL Server, Redis) and service wiring.
- **HybridCacheVisualizer.ApiService**: Exposes endpoints to fetch movie data using different caching strategies (IMemoryCache, IDistributedCache, HybridCache).
- **HybridCacheVisualizer.Consumer**: Simulates load and cache stampede scenarios by stampedeing the API endpoints.
- **Abstractions**: Shared models and cache configuration constants.
- **HybridCacheVisualizer.ServiceDefaults**: Provides common service configuration for health checks, telemetry, and service discovery.
- **HybridCacheVisualizer.Tests**: Integration tests for validating cache behavior and system health.

## API Endpoints

### ApiService

- `GET /movies/{title}`: Fetch movie directly from the database.
- `GET /oldcache/movies/{title}`: Fetch movie using IMemoryCache and IDistributedCache with stampede protection.
- `GET /oldcacheunprotected/movies/{title}`: Fetch movie using IMemoryCache and IDistributedCache without stampede protection.
- `GET /hybridcache/movies/{title}`: Fetch movie using HybridCache.
- `GET /flush`: Flush all cache entries (memory and distributed).

### Consumer

- `GET /stampedeSql`: Simulate multiple requests to the direct database endpoint.
- `GET /stampedeOldWithStampedeProt`: Simulate multiple requests to the old cache endpoint with stampede protection.
- `GET /stampedeOldUnprotected`: Simulate multiple requests to the old cache endpoint without stampede protection.
- `GET /stampedeHybridCache`: Simulate multiple requests to the HybridCache endpoint.
