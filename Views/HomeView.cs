namespace Ormur.Views;

public class HomeView : ContentPage
{
    public HomeView()
    {
        var vm = new HomeViewModel();
        BindingContext = vm;

        Content = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition { Height = GridLength.Star },
                new RowDefinition { Height = GridLength.Auto }
            },
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star }
            },
            Padding = 20,
            RowSpacing = 20,

            Children =
            {
                new StackLayout
                {
                    Spacing = 20,
                    Children =
                    {
                        new Entry()
                            .Placeholder("Enter a task...")
                            .Bind(Entry.TextProperty,
                                getter: static vm => vm.NewTaskText,
                                setter: static (HomeViewModel vm, string code) => vm.NewTaskText = code,
                                mode: BindingMode.TwoWay),

                        new CollectionView()
                            .Bind(ItemsView.ItemsSourceProperty,
                                static (HomeViewModel vm) => vm.Tasks)
                            .ItemTemplate(new DataTemplate(() => new Label()
                                    .Bind(Label.TextProperty, ".")
                                )
                            )
                    }
                }.Row(0),

                new Button
                {
                    HorizontalOptions = LayoutOptions.End,
                    VerticalOptions = LayoutOptions.End,
                    CornerRadius = 50,
                    Margin = new Thickness(0, 0, 0, 20)
                }.Text("+")
                .FontSize(30)
                .BackgroundColor(Colors.Cornsilk)
                .TextColor(Colors.Black)
                .Size(60)
                .BindCommand(static (HomeViewModel vm) => vm.AddTaskCommand)
                .Row(1)
            }
        };
    }
}