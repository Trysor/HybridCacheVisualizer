using Abstractions;
using Microsoft.Data.SqlClient;

namespace HybridCacheVisualizer.ApiService;

public class DatabaseService(SqlConnection connection)
{
    public async Task<Movie?> QueryForMovieByIdAsync(int id, CancellationToken cancel)
    {
        await connection.OpenAsync(cancel);

        var command = connection.CreateCommand();
        command.Parameters.AddWithValue("@ID", id);
        command.CommandText = "SELECT id, title FROM movies where id = @ID";

        var reader = await command.ExecuteReaderAsync(cancel);

        if (await reader.ReadAsync(cancel))
            return new Movie(
                Id: reader.GetInt32(0),
                Title: reader.GetString(1));

        return null;
    }
}
