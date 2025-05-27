using Ormur.Data.SqliteMethods.FolderMethods;

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

        // AddColumnIfNotExists(connection, "Folders", "IsFavorite", "BOOLEAN NOT NULL DEFAULT FALSE");
    }

    /// <summary>
    /// Executes a SQLite Write-Ahead Logging (WAL) checkpoint to transfer all data from the write-ahead log back to
    /// the database file. Afterward, closes the database connection.
    /// </summary>
    /// <remarks>
    /// This method uses the "PRAGMA wal_checkpoint(FULL)" SQLite command to perform a full checkpoint.
    /// This ensures that any unwritten data in the WAL file is safely and explicitly written to the database file.
    /// </remarks>
    public void CheckpointAndClose()
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA wal_checkpoint(FULL);";
        cmd.ExecuteNonQuery();
    }

    public async Task<List<FolderModel>> GetFoldersAsync()
    {
        await Semaphore.WaitAsync();
        try
        {
            var folderMethods = new FoldersMethods();
            return await folderMethods.GetFolder(_dbPath);
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
            var addFolderName = new FoldersMethods();
            await addFolderName.AddFolder(name, _dbPath);
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
            var deleteFolderName = new FoldersMethods();
            await deleteFolderName.DeleteFolder(id, _dbPath);
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
            var toggleFavorite = new FoldersMethods();
            await toggleFavorite.ToggleFavorite(folderId, isFavorite, _dbPath);
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
            var addSubfolderName = new SubFoldersMethods();
            await addSubfolderName.AddSubfolder(parentFolderId, name, _dbPath);
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
            var getSubfolders = new SubFoldersMethods();
            return await getSubfolders.GetSubfolders(parentFolderId, _dbPath);
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
            await using var transaction = await connection.BeginTransactionAsync();

            try
            {
                var deleteSubfolder = new SubFoldersMethods();
                await deleteSubfolder.DeleteSubfolder(id, _dbPath);
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
            var getSubfolder = new SubFoldersMethods();
            return await getSubfolder.GetSubfolder(id, _dbPath);
        }
        finally
        {
            Semaphore.Release();
        }
    }

    // Notes sqlite:
    public async Task<int> AddNoteAsync(int subfolderId, string title, string content)
    {
        await Semaphore.WaitAsync();
        try
        {
            var addNote = new NotesMethods();
            return await addNote.AddNote(subfolderId, title, content, _dbPath);
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
            var updateNote = new NotesMethods();
            await updateNote.UpdateNote(noteId, title, content, _dbPath);
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
            var getNotesBySubfolder = new NotesMethods();
            return await getNotesBySubfolder.GetNotesBySubfolder(subfolderId, _dbPath);
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
            var getNote = new NotesMethods();
            return await getNote.GetNote(noteId, _dbPath);
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
            var deleteNote = new NotesMethods();
            await deleteNote.DeleteNote(noteId, _dbPath);
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
            var addTodoItemInNote = new NoteTodoMethods();
            await addTodoItemInNote.AddTodoItem(noteId, content, position, _dbPath);
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
            var toggleTodoItem = new NoteTodoMethods();
            await toggleTodoItem.ToggleTodoItem(todoId, isCompleted, _dbPath);
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
            var deleteTodoItem = new NoteTodoMethods();
            await deleteTodoItem.DeleteTodoItem(todoId, _dbPath);
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
            var getTodoItemsByNote = new NoteTodoMethods();
            return await getTodoItemsByNote.GetTodoItemsByNote(noteId, _dbPath);
        }
        finally
        {
            Semaphore.Release();
        }
    }

    /// <summary>
    /// Adds a new column to a specified table in the SQLite database if it does not already exist.
    /// </summary>
    /// <param name="connection">The SQLite connection to the database.</param>
    /// <param name="tableName">The name of the table to which the column will be added.</param>
    /// <param name="columnName">The name of the column to be added.</param>
    /// <param name="columnDefinition">The definition of the column, including its data type and constraints.</param>
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
            if (!string.Equals(name, columnName, StringComparison.OrdinalIgnoreCase)) continue;
            columnExists = true;
            break;
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

    /// <summary>
    /// Executes the SQLite "VACUUM" command to rebuild the database file, reducing its size by defragmenting
    /// and reclaiming unused space.
    /// </summary>
    /// <remarks>
    /// This method locks the database using a semaphore to ensure thread safety during the vacuum operation.
    /// The "VACUUM" command creates a new compact database file and transfers all data into it, optimizing
    /// performance and storage efficiency.
    /// </remarks>
    /// <returns>
    /// A task representing the asynchronous operation of vacuuming the database.
    /// </returns>
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