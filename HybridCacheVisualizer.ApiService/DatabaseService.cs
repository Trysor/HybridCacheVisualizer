using HybridCacheVisualizer.Abstractions.DataObjects;
using Microsoft.Data.SqlClient;

namespace HybridCacheVisualizer.ApiService;

public class DatabaseService(SqlConnection connection)
{
    /// <summary>
    /// Asynchronously queries the database for a movie by its unique identifier.
    /// </summary>
    /// <remarks>This method opens a database connection, executes a query to retrieve the movie  with the
    /// specified identifier, and returns the result as a <see cref="Movie"/> object.  If no matching movie is found,
    /// the method returns <see langword="null"/>.</remarks>
    /// <param name="id">The unique identifier of the movie to query.</param>
    /// <param name="cancel">A <see cref="CancellationToken"/> that can be used to cancel the operation.</param>
    /// <returns>A <see cref="Movie"/> object representing the movie with the specified identifier,  or <see langword="null"/> if
    /// no movie with the given identifier exists.</returns>
    public async Task<Movie?> QueryForMovieByIdAsync(int id, CancellationToken cancel)
    {
        await connection.OpenAsync(cancel).ConfigureAwait(false);

        using var command = connection.CreateCommand();
        command.Parameters.AddWithValue("@ID", id);
        command.CommandText = "SELECT id, title FROM movies where id = @ID";

        using var reader = await command.ExecuteReaderAsync(cancel).ConfigureAwait(false);

        if (await reader.ReadAsync(cancel).ConfigureAwait(false))
            return new Movie(
                Id: reader.GetInt32(0),
                Title: reader.GetString(1));

        return null;
    }
}
