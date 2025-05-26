namespace Ormur.Data.SqliteMethods.FolderMethods;

public class CodeSnippetMethods
{
    public async Task<List<CodeSnippetModel>> GetCodeSnippetsByNoteAsync(int noteId, string dbPath)
    {
        await using var connection = new SqliteConnection($"Data Source={dbPath}");
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT Id, NoteId, Language, Content 
            FROM CodeSnippets 
            WHERE NoteId = $noteId 
            ORDER BY Language, Id";

        command.Parameters.AddWithValue("$noteId", noteId);

        var snippets = new List<CodeSnippetModel>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            snippets.Add(new CodeSnippetModel
            {
                Id = reader.GetInt32(0),
                NoteId = reader.GetInt32(1),
                Language = reader.GetString(2),
                Content = reader.GetString(3)
            });
        }

        return snippets;
    }

    public async Task<int> AddCodeSnippetAsync(int noteId, string language, string content, string dbPath)
    {
        await using var connection = new SqliteConnection($"Data Source={dbPath}");
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO CodeSnippets (NoteId, Language, Content)
            VALUES ($noteId, $language, $content);
            SELECT last_insert_rowid();";

        command.Parameters.AddWithValue("$noteId", noteId);
        command.Parameters.AddWithValue("$language", language);
        command.Parameters.AddWithValue("$content", content);

        var id = Convert.ToInt32(await command.ExecuteScalarAsync());
        return id;
    }

    public async Task DeleteCodeSnippetAsync(int snippetId, string dbPath)
    {
        await using var connection = new SqliteConnection($"Data Source={dbPath}");
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM CodeSnippets WHERE Id = $id";
        command.Parameters.AddWithValue("$id", snippetId);

        await command.ExecuteNonQueryAsync();
    }
}