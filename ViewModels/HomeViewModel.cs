namespace Ormur.ViewModels;

public partial class HomeViewModel : ObservableObject
{
    private readonly SqliteConnector _db;
    [ObservableProperty] private string? _newTaskText;
    public ObservableCollection<string?> Tasks { get; set; } = [];

    public HomeViewModel()
    {
        _db = new SqliteConnector();
        _ = LoadTasksAsync();
    }

    private async Task LoadTasksAsync()
    {
        var items = await _db.GetFoldersAsync();
        foreach (var item in items)
        {
            Tasks.Add(item.Name);
        }
    }

    [RelayCommand]
    private async Task AddTaskAsync()
    {
        if (!string.IsNullOrWhiteSpace(NewTaskText))
        {
            await _db.AddFolderAsync(NewTaskText);
            Tasks.Add(NewTaskText);
            NewTaskText = string.Empty;
        }
    }
}