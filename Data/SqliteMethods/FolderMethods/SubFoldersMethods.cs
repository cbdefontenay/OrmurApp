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
}