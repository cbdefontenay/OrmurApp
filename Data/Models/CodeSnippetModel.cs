namespace Ormur.Data.Models;

public class CodeSnippetModel
{
    public int Id { get; set; }
    public int NoteId { get; set; }
    public string Language { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}