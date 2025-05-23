namespace Ormur;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var navigationPage = new NavigationPage(new MainPage());

#if ANDROID
        navigationPage.BarBackgroundColor = Colors.LightBlue;
        navigationPage.BarTextColor = Colors.Black;
#elif WINDOWS
        navigationPage.BarBackgroundColor = Colors.Transparent;
#endif

        return new Window(navigationPage);
    }
}