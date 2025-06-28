using HybridCacheVisualizer.Abstractions.DataObjects;
using HybridCacheVisualizer.Abstractions.Dtos;
using System.Text.Json.Serialization;

namespace HybridCacheVisualizer.Abstractions;

[JsonSerializable(typeof(Movie))]
[JsonSerializable(typeof(StampedeRequest))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase
)]
public sealed partial class SerializationContext : JsonSerializerContext
{
}
