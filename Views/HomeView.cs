namespace Ormur.Views;

public class HomeView : ContentPage
{
    public HomeView()
    {
        Content = new StackLayout
        {
            Children =
            {
                new Label().Text("Hello, World!")
            }
        };
    }
}