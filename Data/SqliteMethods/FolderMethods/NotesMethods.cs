namespace Ormur.Data.SqliteMethods.FolderMethods;

public class NotesMethods
{
    public async Task<int> AddNote(int subfolderId, string title, string content, string dbPath)
    {
        await using var connection = new SqliteConnection($"Data Source={dbPath}");
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

    public async Task UpdateNote(int noteId, string title, string content, string dbPath)
    {
        await using var connection = new SqliteConnection($"Data Source={dbPath}");
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

    public async Task<List<NoteModel>> GetNotesBySubfolder(int subfolderId, string dbPath)
    {
        await using var connection = new SqliteConnection($"Data Source={dbPath}");
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
                }
            );
        }

        return notes;
    }

    /// <summary>
    /// Retrieves a single note from the database by its unique identifier.
    /// </summary>
    /// <param name="noteId">The unique identifier of the note to retrieve.</param>
    /// <param name="dbPath">The file path to the SQLite database.</param>
    /// <returns>A <see cref="NoteModel"/> object representing the retrieved note, or null if no note is found.</returns>
    public async Task<NoteModel?> GetNote(int noteId, string dbPath)
    {
        await using var connection = new SqliteConnection($"Data Source={dbPath}");
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

    /// <summary>
    /// Deletes a note from the database by its unique identifier.
    /// </summary>
    /// <param name="noteId">The unique identifier of the note to delete.</param>
    /// <param name="dbPath">The file path to the SQLite database.</param>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task DeleteNote(int noteId, string dbPath)
    {
        await using var connection = new SqliteConnection($"Data Source={dbPath}");
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Notes WHERE Id = @noteId";
        command.Parameters.AddWithValue("@noteId", noteId);

        await command.ExecuteNonQueryAsync();
    }
}