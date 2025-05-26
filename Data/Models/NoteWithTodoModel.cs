namespace Ormur.Data.Models;

public class NoteWithTodoModel
{
    public NoteModel? Note { get; set; }
    public List<TodoItemModel> TodoItems { get; set; } = [];
    public bool IsExpanded { get; set; }
}