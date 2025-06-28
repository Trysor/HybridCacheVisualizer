using System.ComponentModel;

namespace HybridCacheVisualizer.Abstractions.DataObjects;

[ImmutableObject(true)]
public sealed record ImmutableMovie(int Id, string Title);