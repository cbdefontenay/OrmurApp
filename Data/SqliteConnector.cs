namespace Ormur.Data;

public class SqliteConnector
{
    private static readonly SemaphoreSlim Semaphore = new(1, 1);
    private readonly string _dbPath;

    public SqliteConnector()
    {
        _dbPath = Path.Combine(FileSystem.AppDataDirectory, "ormur.db");
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();

        var walCommand = connection.CreateCommand();
        walCommand.CommandText = "PRAGMA journal_mode=WAL;";
        walCommand.ExecuteNonQuery();

        var tableCmd = connection.CreateCommand();
        tableCmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS Subfolders (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                ParentFolderId INTEGER NOT NULL,
                DateCreated TEXT NOT NULL,
                FOREIGN KEY(ParentFolderId) REFERENCES Folders(Id)
            );";
        tableCmd.ExecuteNonQuery();
    }

    public async Task<List<FolderModel>> GetFoldersAsync()
    {
        await Semaphore.WaitAsync();
        try
        {
            await using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, Name, DateCreated FROM Folders ORDER BY DateCreated DESC";

            var folders = new List<FolderModel>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                folders.Add(new FolderModel
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    DateCreated = DateTime.Parse(reader.GetString(2))
                });
            }

            return folders;
        }
        finally
        {
            Semaphore.Release();
        }
    }

    public async Task AddFolderAsync(string name)
    {
        await Semaphore.WaitAsync();
        try
        {
            await using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "INSERT INTO Folders (Name, DateCreated) VALUES (@name, @dateCreated)";
            command.Parameters.AddWithValue("@name", name);
            command.Parameters.AddWithValue("@dateCreated", DateTime.UtcNow.ToString("o"));

            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            Semaphore.Release();
        }
    }

    public async Task DeleteFolderAsync(int id)
    {
        await Semaphore.WaitAsync();
        try
        {
            await using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM Folders WHERE Id = @id";
            command.Parameters.AddWithValue("@id", id);

            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            Semaphore.Release();
        }
    }

    public async Task AddSubfolderAsync(int parentFolderId, string name)
    {
        await Semaphore.WaitAsync();
        try
        {
            await using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText =
                "INSERT INTO Subfolders (ParentFolderId, Name, DateCreated) VALUES (@parentId, @name, @dateCreated)";
            command.Parameters.AddWithValue("@parentId", parentFolderId);
            command.Parameters.AddWithValue("@name", name);
            command.Parameters.AddWithValue("@dateCreated", DateTime.UtcNow.ToString("o"));

            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            Semaphore.Release();
        }
    }

    public async Task<List<SubfolderModel>> GetSubfoldersAsync(int parentFolderId)
    {
        await Semaphore.WaitAsync();
        try
        {
            await using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText =
                "SELECT Id, Name, ParentFolderId, DateCreated FROM Subfolders WHERE ParentFolderId = @parentId ORDER BY DateCreated DESC";
            command.Parameters.AddWithValue("@parentId", parentFolderId);

            var subfolders = new List<SubfolderModel>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                subfolders.Add(new SubfolderModel
                    {
                        Id = reader.GetInt32(0),
                        Name = reader.GetString(1),
                        ParentFolderId = reader.GetInt32(2),
                        DateCreated = DateTime.Parse(reader.GetString(3))
                    }
                );
            }

            return subfolders;
        }
        finally
        {
            Semaphore.Release();
        }
    }

    public async Task DeleteSubfolderAsync(int id)
    {
        await Semaphore.WaitAsync();
        try
        {
            await using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM Subfolders WHERE Id = @id";
            command.Parameters.AddWithValue("@id", id);

            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            Semaphore.Release();
        }
    }

    public async Task<SubfolderModel?> GetSubfolderAsync(int id)
    {
        await Semaphore.WaitAsync();
        try
        {
            await using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, Name, ParentFolderId, DateCreated FROM Subfolders WHERE Id = @id";
            command.Parameters.AddWithValue("@id", id);

            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new SubfolderModel
                {
                    Id = reader.GetInt32(0),
                    Name = reader.GetString(1),
                    ParentFolderId = reader.GetInt32(2),
                    DateCreated = DateTime.Parse(reader.GetString(3))
                };
            }

            return null;
        }
        finally
        {
            Semaphore.Release();
        }
    }
}
