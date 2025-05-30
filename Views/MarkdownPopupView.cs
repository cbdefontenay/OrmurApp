namespace Ormur.Views;

// TODO: implement popup to explain user markdown syntax
public class MarkdownPopupView : Popup
{
    public MarkdownPopupView()
    {
        Content = new VerticalStackLayout
        {
            Children =
            {
                new Label
                {
                    Text = "This is a very important message!"
                }
            }
        };
    }
}