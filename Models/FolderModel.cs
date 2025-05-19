namespace Ormur.Models;

public class FolderModel
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public DateTime DateCreated { get; set; } = DateTime.UtcNow;
}