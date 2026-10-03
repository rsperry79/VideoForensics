using Microsoft.Maui;
using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;
using Microsoft.Extensions.Logging;

using VideoForensics.Utils.LoggerViewer.Maui.Pages;
using VideoForensics.Utils.LoggerViewer.Maui.Services;
using VideoForensics.Utils.LoggerViewer.Maui.ViewModels;

namespace VideoForensics.Utils.LoggerViewer.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            })
            .ConfigureServices()
            .ConfigureViewModels()
            .ConfigurePages();

        return builder.Build();
    }

    private static MauiAppBuilder ConfigureServices(this MauiAppBuilder builder)
    {
        builder.Services.AddSingleton<LogReaderService>();

        return builder;
    }

    private static MauiAppBuilder ConfigureViewModels(this MauiAppBuilder builder)
    {
        builder.Services.AddSingleton<LogViewerViewModel>();

        return builder;
    }

    private static MauiAppBuilder ConfigurePages(this MauiAppBuilder builder)
    {
        builder.Services.AddSingleton<MainPage>();

        return builder;
    }
}
