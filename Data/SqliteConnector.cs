using Path = System.IO.Path;

namespace Ormur.Data;

public class SqliteConnector
{
    private int _deletionCount = 0;
    private static readonly SemaphoreSlim Semaphore = new(1, 1);
    private readonly string _dbPath;
    public int GetPendingVacuumCount() => _deletionCount;
    public void ResetVacuumCount() => Interlocked.Exchange(ref _deletionCount, 0);

    public SqliteConnector()
    {
        _dbPath = Path.Combine(FileSystem.AppDataDirectory, "ormur.db");
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();

        var fkCommand = connection.CreateCommand();
        fkCommand.CommandText = "PRAGMA foreign_keys = ON;";
        fkCommand.ExecuteNonQuery();

        var walCommand = connection.CreateCommand();
        walCommand.CommandText = "PRAGMA journal_mode=WAL;";
        walCommand.ExecuteNonQuery();

        var tableCmd = connection.CreateCommand();
        tableCmd.CommandText = @"
    CREATE TABLE IF NOT EXISTS Folders (
        Id INTEGER PRIMARY KEY AUTOINCREMENT,
        Name TEXT NOT NULL,
        IsFavorite BOOLEAN NOT NULL DEFAULT FALSE,
        DateCreated TEXT NOT NULL
    );

    CREATE TABLE IF NOT EXISTS Subfolders (
        Id INTEGER PRIMARY KEY AUTOINCREMENT,
        Name TEXT NOT NULL,
        ParentFolderId INTEGER NOT NULL,
        DateCreated TEXT NOT NULL,
        FOREIGN KEY(ParentFolderId) REFERENCES Folders(Id) ON DELETE CASCADE
    );

    CREATE TABLE IF NOT EXISTS Notes (
        Id INTEGER PRIMARY KEY AUTOINCREMENT,
        SubfolderId INTEGER NOT NULL,
        Title TEXT NOT NULL,
        Content TEXT NOT NULL,
        DateCreated TEXT NOT NULL,
        DateModified TEXT NOT NULL,
        FOREIGN KEY(SubfolderId) REFERENCES Subfolders(Id) ON DELETE CASCADE
    );

    CREATE TABLE IF NOT EXISTS TodoItems (
        Id INTEGER PRIMARY KEY AUTOINCREMENT,
        NoteId INTEGER NOT NULL,
        Content TEXT NOT NULL,
        IsCompleted BOOLEAN NOT NULL DEFAULT FALSE,
        Position INTEGER NOT NULL,
        FOREIGN KEY(NoteId) REFERENCES Notes(Id) ON DELETE CASCADE
    );";
        tableCmd.ExecuteNonQuery();

        AddColumnIfNotExists(connection, "Folders", "IsFavorite", "BOOLEAN NOT NULL DEFAULT FALSE");
    }


    public async Task<List<FolderModel>> GetFoldersAsync()
    {
        await Semaphore.WaitAsync();
        try
        {
            await using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, Name, IsFavorite, DateCreated FROM Folders ORDER BY DateCreated DESC";

            var folders = new List<FolderModel>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                folders.Add(new FolderModel
                    {
                        Id = reader.GetInt32(0),
                        Name = reader.GetString(1),
                        IsFavorite = reader.GetBoolean(2),
                        DateCreated = DateTime.Parse(reader.GetString(3))
                    }
                );
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
            command.CommandText =
                "INSERT INTO Folders (Name, IsFavorite, DateCreated) VALUES (@name, @isFavorite, @dateCreated)";
            command.Parameters.AddWithValue("@isFavorite", false);
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

            // Start a transaction
            await using var transaction = await connection.BeginTransactionAsync();

            try
            {
                // First delete all notes and their todo items in all subfolders
                var subfoldersCmd = connection.CreateCommand();
                subfoldersCmd.CommandText = "SELECT Id FROM Subfolders WHERE ParentFolderId = @folderId";
                subfoldersCmd.Parameters.AddWithValue("@folderId", id);

                var subfolderIds = new List<int>();
                await using (var reader = await subfoldersCmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        subfolderIds.Add(reader.GetInt32(0));
                    }
                }

                foreach (var subfolderId in subfolderIds)
                {
                    var deleteTodosCmd = connection.CreateCommand();
                    deleteTodosCmd.CommandText = @"
                    DELETE FROM TodoItems 
                    WHERE NoteId IN (SELECT Id FROM Notes WHERE SubfolderId = @subfolderId)";
                    deleteTodosCmd.Parameters.AddWithValue("@subfolderId", subfolderId);
                    await deleteTodosCmd.ExecuteNonQueryAsync();

                    var deleteNotesCmd = connection.CreateCommand();
                    deleteNotesCmd.CommandText = "DELETE FROM Notes WHERE SubfolderId = @subfolderId";
                    deleteNotesCmd.Parameters.AddWithValue("@subfolderId", subfolderId);
                    await deleteNotesCmd.ExecuteNonQueryAsync();
                }

                var deleteSubfoldersCmd = connection.CreateCommand();
                deleteSubfoldersCmd.CommandText = "DELETE FROM Subfolders WHERE ParentFolderId = @folderId";
                deleteSubfoldersCmd.Parameters.AddWithValue("@folderId", id);
                await deleteSubfoldersCmd.ExecuteNonQueryAsync();

                var deleteFolderCmd = connection.CreateCommand();
                deleteFolderCmd.CommandText = "DELETE FROM Folders WHERE Id = @folderId";
                deleteFolderCmd.Parameters.AddWithValue("@folderId", id);
                await deleteFolderCmd.ExecuteNonQueryAsync();

                await transaction.CommitAsync();
                Interlocked.Increment(ref _deletionCount);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        finally
        {
            Semaphore.Release();
        }
    }

    public async Task ToggleFavoriteAsync(int folderId, bool isFavorite)
    {
        await Semaphore.WaitAsync();
        try
        {
            await using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "UPDATE Folders SET IsFavorite = @isFavorite WHERE Id = @id";
            command.Parameters.AddWithValue("@isFavorite", isFavorite);
            command.Parameters.AddWithValue("@id", folderId);

            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            Semaphore.Release();
        }
    }

    public async Task<List<FolderModel>> GetAllFavoritesAsync()
    {
        await Semaphore.WaitAsync();
        try
        {
            await using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText =
                "SELECT Id, Name, IsFavorite, DateCreated FROM Folders WHERE IsFavorite = 1 ORDER BY DateCreated DESC";

            var favorites = new List<FolderModel>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                favorites.Add(new FolderModel
                    {
                        Id = reader.GetInt32(0),
                        Name = reader.GetString(1),
                        IsFavorite = reader.GetBoolean(2),
                        DateCreated = DateTime.Parse(reader.GetString(3))
                    }
                );
            }

            return favorites;
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

            // Start a transaction
            await using var transaction = await connection.BeginTransactionAsync();

            try
            {
                // First delete all notes in this subfolder
                var deleteTodosCmd = connection.CreateCommand();
                deleteTodosCmd.CommandText = @"
                DELETE FROM TodoItems 
                WHERE NoteId IN (SELECT Id FROM Notes WHERE SubfolderId = @subfolderId)";
                deleteTodosCmd.Parameters.AddWithValue("@subfolderId", id);
                await deleteTodosCmd.ExecuteNonQueryAsync();

                // Then delete all notes in this subfolder
                var deleteNotesCmd = connection.CreateCommand();
                deleteNotesCmd.CommandText = "DELETE FROM Notes WHERE SubfolderId = @subfolderId";
                deleteNotesCmd.Parameters.AddWithValue("@subfolderId", id);
                await deleteNotesCmd.ExecuteNonQueryAsync();

                // Finally delete the subfolder itself
                var deleteSubfolderCmd = connection.CreateCommand();
                deleteSubfolderCmd.CommandText = "DELETE FROM Subfolders WHERE Id = @id";
                deleteSubfolderCmd.Parameters.AddWithValue("@id", id);
                await deleteSubfolderCmd.ExecuteNonQueryAsync();

                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
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

    // Notes sqlite:
    // Add these methods to your SqliteConnector class
    public async Task<int> AddNoteAsync(int subfolderId, string title, string content)
    {
        await Semaphore.WaitAsync();
        try
        {
            await using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
            INSERT INTO Notes (SubfolderId, Title, Content, DateCreated, DateModified)
            VALUES (@subfolderId, @title, @content, @dateCreated, @dateModified);
            SELECT last_insert_rowid();";

            command.Parameters.AddWithValue("@subfolderId", subfolderId);
            command.Parameters.AddWithValue("@title", title);
            command.Parameters.AddWithValue("@content", content);
            command.Parameters.AddWithValue("@dateCreated", DateTime.UtcNow.ToString("o"));
            command.Parameters.AddWithValue("@dateModified", DateTime.UtcNow.ToString("o"));

            return Convert.ToInt32(await command.ExecuteScalarAsync());
        }
        finally
        {
            Semaphore.Release();
        }
    }

    public async Task UpdateNoteAsync(int noteId, string title, string content)
    {
        await Semaphore.WaitAsync();
        try
        {
            await using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
            UPDATE Notes
            SET Title = @title,
                Content = @content,
                DateModified = @dateModified
            WHERE Id = @noteId";

            command.Parameters.AddWithValue("@noteId", noteId);
            command.Parameters.AddWithValue("@title", title);
            command.Parameters.AddWithValue("@content", content);
            command.Parameters.AddWithValue("@dateModified", DateTime.UtcNow.ToString("o"));

            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            Semaphore.Release();
        }
    }

    public async Task<List<NoteModel>> GetNotesBySubfolderAsync(int subfolderId)
    {
        await Semaphore.WaitAsync();
        try
        {
            await using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
            SELECT Id, Title, Content, DateCreated, DateModified
            FROM Notes
            WHERE SubfolderId = @subfolderId
            ORDER BY DateModified DESC";

            command.Parameters.AddWithValue("@subfolderId", subfolderId);

            var notes = new List<NoteModel>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                notes.Add(new NoteModel
                {
                    Id = reader.GetInt32(0),
                    Title = reader.GetString(1),
                    Content = reader.GetString(2),
                    DateCreated = DateTime.Parse(reader.GetString(3)),
                    DateModified = DateTime.Parse(reader.GetString(4))
                });
            }

            return notes;
        }
        finally
        {
            Semaphore.Release();
        }
    }

    public async Task<NoteModel?> GetNoteAsync(int noteId)
    {
        await Semaphore.WaitAsync();
        try
        {
            await using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT Id, Title, Content, DateCreated, DateModified FROM Notes WHERE Id = @noteId";
            command.Parameters.AddWithValue("@noteId", noteId);

            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new NoteModel
                {
                    Id = reader.GetInt32(0),
                    Title = reader.GetString(1),
                    Content = reader.GetString(2),
                    DateCreated = DateTime.Parse(reader.GetString(3)),
                    DateModified = DateTime.Parse(reader.GetString(4))
                };
            }

            return null;
        }
        finally
        {
            Semaphore.Release();
        }
    }

    public async Task DeleteNoteAsync(int noteId)
    {
        await Semaphore.WaitAsync();
        try
        {
            await using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM Notes WHERE Id = @noteId";
            command.Parameters.AddWithValue("@noteId", noteId);

            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            Semaphore.Release();
        }
    }

    public async Task AddTodoItemAsync(int noteId, string content, int position)
    {
        await Semaphore.WaitAsync();
        try
        {
            await using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
            INSERT INTO TodoItems (NoteId, Content, IsCompleted, Position)
            VALUES (@noteId, @content, @isCompleted, @position)";

            command.Parameters.AddWithValue("@noteId", noteId);
            command.Parameters.AddWithValue("@content", content);
            command.Parameters.AddWithValue("@isCompleted", false);
            command.Parameters.AddWithValue("@position", position);

            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            Semaphore.Release();
        }
    }

    public async Task ToggleTodoItemAsync(int todoId, bool isCompleted)
    {
        await Semaphore.WaitAsync();
        try
        {
            await using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync().ConfigureAwait(false);

            var command = connection.CreateCommand();
            command.CommandText = "UPDATE TodoItems SET IsCompleted = @isCompleted WHERE Id = @todoId";
            command.Parameters.AddWithValue("@todoId", todoId);
            command.Parameters.AddWithValue("@isCompleted", isCompleted);

            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
        finally
        {
            Semaphore.Release();
        }
    }

    public async Task DeleteTodoItemAsync(int todoId)
    {
        await Semaphore.WaitAsync();
        try
        {
            await using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM TodoItems WHERE Id = @todoId";
            command.Parameters.AddWithValue("@todoId", todoId);

            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            Semaphore.Release();
        }
    }

    public async Task<List<TodoItemModel>> GetTodoItemsByNoteAsync(int noteId)
    {
        await Semaphore.WaitAsync();
        try
        {
            await using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText =
                "SELECT Id, Content, IsCompleted, Position FROM TodoItems WHERE NoteId = @noteId ORDER BY Position";
            command.Parameters.AddWithValue("@noteId", noteId);

            var todos = new List<TodoItemModel>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                todos.Add(new TodoItemModel
                {
                    Id = reader.GetInt32(0),
                    Content = reader.GetString(1),
                    IsCompleted = reader.GetBoolean(2),
                    Position = reader.GetInt32(3)
                });
            }

            return todos;
        }
        finally
        {
            Semaphore.Release();
        }
    }

    private void AddColumnIfNotExists(SqliteConnection connection, string tableName, string columnName,
        string columnDefinition)
    {
        var pragmaCmd = connection.CreateCommand();
        pragmaCmd.CommandText = $"PRAGMA table_info({tableName});";
        using var reader = pragmaCmd.ExecuteReader();

        var columnExists = false;
        while (reader.Read())
        {
            var name = reader.GetString(1);
            if (string.Equals(name, columnName, StringComparison.OrdinalIgnoreCase))
            {
                columnExists = true;
                break;
            }
        }

        switch (columnExists)
        {
            case false:
            {
                var alterCmd = connection.CreateCommand();
                alterCmd.CommandText = $"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition};";
                alterCmd.ExecuteNonQuery();
                break;
            }
        }
    }

    public async Task VacuumDatabaseAsync()
    {
        await Semaphore.WaitAsync();
        try
        {
            await using var connection = new SqliteConnection($"Data Source={_dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "VACUUM;";
            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            Semaphore.Release();
        }
    }
}