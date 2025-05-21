namespace Ormur.Models;

public class NoteModel
{
    public int Id { get; set; }
    public int SubfolderId { get; set; }
    public string? Title { get; set; }
    public string? Content { get; set; }
    public DateTime DateCreated { get; set; }
    public DateTime DateModified { get; set; }
    public List<TodoItemModel> TodoItems { get; set; } = [];
}
