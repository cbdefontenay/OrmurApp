using Color = Microsoft.Maui.Graphics.Color;

namespace Ormur.Views;

public class AboutOrmurView : ContentPage
{
    public AboutOrmurView()
    {
        NavigationPage.SetHasNavigationBar(this, true);
        StatusBar.SetColor(Colors.LightBlue);
        BackgroundColor = Color.FromRgb(245, 251, 245);

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = new Thickness(24),
                Spacing = 20,
                Children =
                {
                    new Label
                        {
                            HorizontalTextAlignment = TextAlignment.Center,
                            FontAttributes = FontAttributes.Bold
                        }
                        .Text("Über Ormur")
                        .FontSize(32)
                        .TextColor(Colors.Black),

                    new Border
                    {
                        StrokeShape = new RoundRectangle { CornerRadius = 16 },
                        BackgroundColor = Colors.White,
                        StrokeThickness = 0,
                        Padding = new Thickness(20),
                        Content = new VerticalStackLayout
                        {
                            Spacing = 16,
                            Children =
                            {
                                new Label
                                {
                                    Text =
                                        "Ormur ist eine einfache Notizapplikation, die deine Daten nicht verwendet, denn deine Daten werden lokal gespeichert mit Hilfe einer SQLite-Datenbank.",
                                    FontSize = 16,
                                    TextColor = Colors.Black
                                },
                                new Label
                                {
                                    Text =
                                        "Sicherheit zuerst – das könnte das Motto der App werden, denn was lokal ist, können andere nicht sehen oder benutzen.",
                                    FontSize = 16,
                                    TextColor = Colors.Black
                                }
                            }
                        }
                    }
                }
            }
        };
    }
}