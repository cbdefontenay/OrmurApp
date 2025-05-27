namespace Ormur.Data.SqliteMethods.FolderMethods;

public class FoldersMethods
{
    private static readonly SemaphoreSlim Semaphore = new(1, 1);
    private int _deletionCount = 0;

    public async Task<List<FolderModel>> GetFolder(string dbPath)
    {
        await Semaphore.WaitAsync();
        try
        {
            await using var connection = new SqliteConnection($"Data Source={dbPath}");
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

    public async Task AddFolder(string name, string dbPath)
    {
        await using var connection = new SqliteConnection($"Data Source={dbPath}");
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText =
            "INSERT INTO Folders (Name, IsFavorite, DateCreated) VALUES (@name, @isFavorite, @dateCreated)";
        command.Parameters.AddWithValue("@isFavorite", false);
        command.Parameters.AddWithValue("@name", name);
        command.Parameters.AddWithValue("@dateCreated", DateTime.UtcNow.ToString("o"));

        await command.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Deletes a folder and its associated subfolders, notes, and todoitems from the database.
    /// </summary>
    /// <param name="id">The unique identifier of the folder to delete.</param>
    /// <param name="dbPath">The file path to the SQLite database.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task DeleteFolder(int id, string dbPath)
    {
        await Semaphore.WaitAsync();
        try
        {
            await using var connection = new SqliteConnection($"Data Source={dbPath}");
            await connection.OpenAsync();

            // Enable foreign keys if not already enabled
            var fkCmd = connection.CreateCommand();
            fkCmd.CommandText = "PRAGMA foreign_keys = ON;";
            await fkCmd.ExecuteNonQueryAsync();

            // Use a single transaction for all operations
            await using var transaction = await connection.BeginTransactionAsync();

            try
            {
                // First delete all TodoItems associated with notes in subfolders
                var deleteTodosCmd = connection.CreateCommand();
                deleteTodosCmd.CommandText = @"
                DELETE FROM TodoItems 
                WHERE NoteId IN (
                    SELECT n.Id FROM Notes n
                    JOIN Subfolders s ON n.SubfolderId = s.Id
                    WHERE s.ParentFolderId = @folderId
                )";
                deleteTodosCmd.Parameters.AddWithValue("@folderId", id);
                await deleteTodosCmd.ExecuteNonQueryAsync();

                // Then delete all Notes in subfolders
                var deleteNotesCmd = connection.CreateCommand();
                deleteNotesCmd.CommandText = @"
                DELETE FROM Notes 
                WHERE SubfolderId IN (
                    SELECT Id FROM Subfolders WHERE ParentFolderId = @folderId
                )";
                deleteNotesCmd.Parameters.AddWithValue("@folderId", id);
                await deleteNotesCmd.ExecuteNonQueryAsync();

                // Then delete all Subfolders
                var deleteSubfoldersCmd = connection.CreateCommand();
                deleteSubfoldersCmd.CommandText = "DELETE FROM Subfolders WHERE ParentFolderId = @folderId";
                deleteSubfoldersCmd.Parameters.AddWithValue("@folderId", id);
                await deleteSubfoldersCmd.ExecuteNonQueryAsync();

                // Finally delete the folder itself
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

    public async Task ToggleFavorite(int folderId, bool isFavorite, string dbPath)
    {
        await using var connection = new SqliteConnection($"Data Source={dbPath}");
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "UPDATE Folders SET IsFavorite = @isFavorite WHERE Id = @id";
        command.Parameters.AddWithValue("@isFavorite", isFavorite);
        command.Parameters.AddWithValue("@id", folderId);

        await command.ExecuteNonQueryAsync();
    }
}