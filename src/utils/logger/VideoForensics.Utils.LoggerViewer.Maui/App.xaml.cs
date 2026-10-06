using Microsoft.Maui;
using Microsoft.Maui.Controls;

namespace VideoForensics.Utils.LoggerViewer.Maui;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = base.CreateWindow(activationState);

        // Get the main page with DI
        var mainPage = this.Handler?.MauiContext?.Services.GetRequiredService<Pages.MainPage>();
        if (mainPage != null)
        {
            var navPage = new NavigationPage(mainPage)
            {
                BarBackgroundColor = Colors.White,
                BarTextColor = Colors.Black
            };
            window.Page = navPage;
        }

        return window;
    }
}
