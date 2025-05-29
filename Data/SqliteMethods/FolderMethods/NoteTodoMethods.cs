namespace Ormur.Data.SqliteMethods.FolderMethods;

/// <summary>
/// Provides methods for managing todoitems within notes in an SQLite database.
/// </summary>
public class NoteTodoMethods
{
    /// <summary>
    /// Adds a todoitem to a note in the database.
    /// </summary>
    /// <param name="noteId">The ID of the note to which the todoitem will be added.</param>
    /// <param name="content">The content or description of the todoitem.</param>
    /// <param name="position">The position or order of the todoitem in the list.</param>
    /// <param name="dbPath">The file path to the SQLite database.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task AddTodoItem(int noteId, string content, int position, string dbPath)
    {
        await using var connection = new SqliteConnection($"Data Source={dbPath}");
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

    public async Task ToggleTodoItem(int todoId, bool isCompleted, string dbPath)
    {
        await using var connection = new SqliteConnection($"Data Source={dbPath}");
        await connection.OpenAsync().ConfigureAwait(false);

        var command = connection.CreateCommand();
        command.CommandText = "UPDATE TodoItems SET IsCompleted = @isCompleted WHERE Id = @todoId";
        command.Parameters.AddWithValue("@todoId", todoId);
        command.Parameters.AddWithValue("@isCompleted", isCompleted);

        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }
    
    public async Task DeleteTodoItem(int todoId, string dbPath)
    {
            await using var connection = new SqliteConnection($"Data Source={dbPath}");
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM TodoItems WHERE Id = @todoId";
            command.Parameters.AddWithValue("@todoId", todoId);

            await command.ExecuteNonQueryAsync();

    }
    
    public async Task<List<TodoItemModel>> GetTodoItemsByNote(int noteId, string dbPath)
    {

            await using var connection = new SqliteConnection($"Data Source={dbPath}");
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
    
    public async Task UpdateTodoItem(int todoId, string content, bool isCompleted, int position, string dbPath)
    {
        await using var connection = new SqliteConnection($"Data Source={dbPath}");
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = @"
        UPDATE TodoItems 
        SET Content = @content, 
            IsCompleted = @isCompleted, 
            Position = @position 
        WHERE Id = @todoId";
    
        command.Parameters.AddWithValue("@content", content);
        command.Parameters.AddWithValue("@isCompleted", isCompleted);
        command.Parameters.AddWithValue("@position", position);
        command.Parameters.AddWithValue("@todoId", todoId);

        await command.ExecuteNonQueryAsync();
    }
}