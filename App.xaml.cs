namespace Ormur;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        var language = Preferences.Get("language", "de-DE");
        var culture = new CultureInfo(language);
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
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