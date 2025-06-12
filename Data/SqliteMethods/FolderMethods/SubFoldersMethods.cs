namespace Ormur.Data.SqliteMethods.FolderMethods;

public class SubFoldersMethods
{
    public async Task AddSubfolder(int parentFolderId, string name, string dbPath)
    {
        await using var connection = new SqliteConnection($"Data Source={dbPath}");
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText =
            "INSERT INTO Subfolders (ParentFolderId, Name, DateCreated) VALUES (@parentId, @name, @dateCreated)";
        command.Parameters.AddWithValue("@parentId", parentFolderId);
        command.Parameters.AddWithValue("@name", name);
        command.Parameters.AddWithValue("@dateCreated", DateTime.UtcNow.ToString("o"));

        await command.ExecuteNonQueryAsync();
    }

    public async Task<List<SubfolderModel>> GetSubfolders(int parentFolderId, string dbPath)
    {
        await using var connection = new SqliteConnection($"Data Source={dbPath}");
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

    /// <summary>
    /// Deletes a subfolder along with its associated notes and tasks from the database.
    /// </summary>
    /// <param name="id">The ID of the subfolder to be deleted.</param>
    /// <param name="dbPath">The file path of the SQLite database.</param>
    /// <returns>A task representing the asynchronous delete operation.</returns>
    public async Task DeleteSubfolder(int id, string dbPath)
    {
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromSeconds(10));

        await using var connection = new SqliteConnection($"Data Source={dbPath}");
        await connection.OpenAsync(cts.Token);

        // Enable foreign key support
        var fkCmd = connection.CreateCommand();
        fkCmd.CommandText = "PRAGMA foreign_keys = ON;";
        await fkCmd.ExecuteNonQueryAsync(cts.Token);

        await using var transaction = await connection.BeginTransactionAsync(cts.Token);

        try
        {
            // First delete todos (let foreign keys handle the rest)
            var deleteTodosCmd = connection.CreateCommand();
            deleteTodosCmd.CommandText = @"
            DELETE FROM TodoItems 
            WHERE NoteId IN (SELECT Id FROM Notes WHERE SubfolderId = @subfolderId)";
            deleteTodosCmd.Parameters.AddWithValue("@subfolderId", id);
            await deleteTodosCmd.ExecuteNonQueryAsync(cts.Token);

            // Then delete the subfolder (cascade will handle notes)
            var deleteSubfolderCmd = connection.CreateCommand();
            deleteSubfolderCmd.CommandText = "DELETE FROM Subfolders WHERE Id = @id";
            deleteSubfolderCmd.Parameters.AddWithValue("@id", id);
            await deleteSubfolderCmd.ExecuteNonQueryAsync(cts.Token);

            await transaction.CommitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            try { await transaction.RollbackAsync(cts.Token); } catch { /* Ignore */ }
            throw new TimeoutException("Subfolder deletion timed out");
        }
        catch (Exception)
        {
            try { await transaction.RollbackAsync(cts.Token); } catch { /* Ignore */ }
            throw;
        }
    }

    public async Task<SubfolderModel?> GetSubfolder(int id, string dbPath)
    {
        await using var connection = new SqliteConnection($"Data Source={dbPath}");
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
}