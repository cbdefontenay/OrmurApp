namespace Ormur.Views;

public class AboutOrmurView : ContentPage
{
    public AboutOrmurView()
    {
        Content = new StackLayout
        {
            new Label().Text("About Ormur")
        };
    }
}