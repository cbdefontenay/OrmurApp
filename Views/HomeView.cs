namespace Ormur.Views;

public class HomeView : ContentPage
{
    public HomeView()
    {
        var vm = new HomeViewModel();
        BindingContext = vm;

        Content = new StackLayout
        {
            Spacing = 20,
            Padding = 20,

            Children =
            {
                new Entry()
                    .Placeholder("Enter a task...")
                    .Bind(Entry.TextProperty,
                        getter: static vm => vm.NewTaskText,
                        setter: static (HomeViewModel vm, string code) => vm.NewTaskText = code,
                        mode: BindingMode.TwoWay),

                new Button()
                    .Text("Add Task")
                    .BindCommand(static (HomeViewModel vm) => vm.AddTaskCommand),

                new CollectionView()
                    .Bind(ItemsView.ItemsSourceProperty,
                        static (HomeViewModel vm) => vm.Tasks)
                    .ItemTemplate(new DataTemplate(() => new Label()
                            .Bind(Label.TextProperty, ".")
                        )
                    )
            }
        };
    }
}