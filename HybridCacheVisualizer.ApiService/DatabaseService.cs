using Abstractions;
using Microsoft.Data.SqlClient;

namespace HybridCacheVisualizer.ApiService;

public static class DatabaseService
{
    public static async Task<Movie?> QueryDatabase(SqlConnection connection, string title)
    {

        await connection.OpenAsync();
        var command = connection.CreateCommand();

        command.Parameters.AddWithValue("@TITLE", title);

        command.CommandText = "SELECT * FROM movies where title = @TITLE";
        var reader = await command.ExecuteReaderAsync();

        if (await reader.ReadAsync())
            return new Movie(reader.GetString(1));

        return null;
    }
}
