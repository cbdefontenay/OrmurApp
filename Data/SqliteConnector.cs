using Ormur.Models;

namespace Ormur.Data;

public class SqliteConnector
{
    private readonly string _dbPath;

    public SqliteConnector()
    {
        _dbPath = Path.Combine(FileSystem.AppDataDirectory, "folder.db");
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();

        var tableCmd = connection.CreateCommand();
        tableCmd.CommandText =
            @"CREATE TABLE IF NOT EXISTS Folders (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL
              );";
        tableCmd.ExecuteNonQuery();
    }

    public async Task<List<FolderModel>> GetFoldersAsync()
    {
        await using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Text FROM Folders";

        var tasks = new List<FolderModel>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tasks.Add(new FolderModel
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1)
                }
            );
        }

        return tasks;
    }

    public async Task AddFolderAsync(string name)
    {
        await using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO Folders (Name) VALUES (@name)";
        command.Parameters.AddWithValue("@name", name);

        await command.ExecuteNonQueryAsync();
    }
}