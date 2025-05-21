namespace Ormur.Models;

public class TodoItemModel
{
    public int Id { get; set; }
    public int NoteId { get; set; }
    public string Content { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public int Position { get; set; }
}