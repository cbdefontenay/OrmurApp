namespace Ormur.Views;

public class SettingsView : ContentPage
{
    public SettingsView()
    {
        Content = new StackLayout
        {
            Children =
            {
                new Label().Text("Settings")
            }
        };
    }
}