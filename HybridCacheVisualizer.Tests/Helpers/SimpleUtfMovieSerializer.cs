using HybridCacheVisualizer.Abstractions.DataObjects;
using Microsoft.Extensions.Caching.Hybrid;
using System.Buffers;
using System.Text;

namespace HybridCacheVisualizer.Tests.Helpers;

/// <summary>
/// Provides a simple serializer and deserializer for <see cref="Movie"/> objects using UTF-8 encoding.
/// </summary>
/// <remarks>This serializer encodes the <see cref="Movie.Id"/> as a 4-byte integer and the <see
/// cref="Movie.Title"/>  as a UTF-8 encoded string. The deserialization process expects the same format.</remarks>
internal class SimpleUtfMovieSerializer : IHybridCacheSerializer<Movie>
{
    public int DeserializeCallCount { get; private set; }
    public int SerializeCallCount { get; private set; }

    public Movie Deserialize(ReadOnlySequence<byte> source)
    {
        DeserializeCallCount++;

        var span = source.FirstSpan;
        int id = BitConverter.ToInt32(span[..4]); // Read the ID (first 4 bytes)
        string title = Encoding.UTF8.GetString(span[4..]); // Read the title (remaining bytes)

        return new Movie(id, title);
    }

    public void Serialize(Movie value, IBufferWriter<byte> target)
    {
        SerializeCallCount++;

        BitConverter.TryWriteBytes(target.GetSpan(4), value.Id); // Write the ID (first 4 bytes)
        target.Advance(4); // Advance the buffer writer
        target.Write(Encoding.UTF8.GetBytes(value.Title)); // Get UTF-8 bytes for the title
    }
}
