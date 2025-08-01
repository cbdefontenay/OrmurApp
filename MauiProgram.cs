﻿namespace Ormur;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .UseMauiCommunityToolkitMarkup()
            .ConfigureFonts(fonts => { fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular"); });

        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddFluentUIComponents();
        builder.Services.AddLocalization();
        builder.Services.AddSingleton<SqliteConnector>();
        builder.Services.AddHostedService<DatabaseMaintenanceService>();
        builder.Services.AddSingleton<SyncService>();
        builder.Services.AddScoped<ThemeService>();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}