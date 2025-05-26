namespace Ormur.Data.Models;

public class SubfolderModel
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public int ParentFolderId { get; set; }
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;
}

public class FolderWithSubfolders
{
    public FolderModel Folder { get; set; } = new();
    public List<SubfolderModel> Subfolders { get; set; } = new();
    public bool IsExpanded { get; set; } = false;
}