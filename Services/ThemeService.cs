namespace Ormur.Services;

public class ThemeService(IJSRuntime jsRuntime)
{
    private const string ThemeKey = "app-theme-preference";
    
    public event Action? OnThemeChanged;
    public string CurrentTheme { get; private set; } = "default";

    public async Task InitializeAsync()
    {
        CurrentTheme = await GetStoredTheme() ?? "default";
        await ApplyTheme(CurrentTheme);
    }

    public async Task SetThemeAsync(string theme)
    {
        CurrentTheme = theme;
        await ApplyTheme(theme);
        await StoreTheme(theme);
        OnThemeChanged?.Invoke();
    }

    private async Task<string?> GetStoredTheme()
    {
        try
        {
            return await jsRuntime.InvokeAsync<string>("localStorage.getItem", ThemeKey);
        }
        catch
        {
            return null;
        }
    }

    private async Task StoreTheme(string theme)
    {
        try
        {
            await jsRuntime.InvokeVoidAsync("localStorage.setItem", ThemeKey, theme);
        }
        catch
        {
            // Storage might be unavailable in some environments
        }
    }

    private async Task ApplyTheme(string theme)
    {
        try
        {
            await jsRuntime.InvokeVoidAsync("applyTheme", theme);
        }
        catch
        {
            // Handle JS interop failure
        }
    }
}