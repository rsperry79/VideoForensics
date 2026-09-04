using Microsoft.Extensions.DependencyInjection;
using VideoForensics.Client.Common;
using VideoForensics.MauiApp.AppLock;

namespace VideoForensics.MauiApp;

public partial class App : Application
{
    private readonly IServiceProvider _services;
    private DateTime? _backgroundedAtUtc;

    public App(IServiceProvider services)
    {
        InitializeComponent();
        _services = services;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window { Title = "VideoForensics" };
        window.Page = BuildLockPage(window);

        // App-lock idle timeout (plan §5.9): re-lock after the configured time spent backgrounded,
        // not on every momentary focus loss (Deactivated fires on plain alt-tab, which would be
        // disruptive at anything but the "immediately" setting) - Stopped/Resumed track actual
        // backgrounding (minimize/suspend), matching what the settings screen's wording promises.
        window.Stopped += (_, _) => _backgroundedAtUtc = DateTime.UtcNow;
        window.Resumed += (_, _) =>
        {
            if (_backgroundedAtUtc is null)
            {
                return;
            }

            var elapsed = DateTime.UtcNow - _backgroundedAtUtc.Value;
            var timeout = _services.GetRequiredService<IAppLockPreferencesStore>().GetIdleLockTimeout();
            _backgroundedAtUtc = null;

            if (elapsed >= timeout)
            {
                window.Page = BuildLockPage(window);
            }
        };

        return window;
    }

    private ContentPage BuildLockPage(Window window)
    {
        var authGate = _services.GetRequiredService<ILocalAuthGate>();
        var lockPage = new AppLockPage(authGate);
        lockPage.Unlocked += (_, _) => window.Page = new MainPage();
        return lockPage;
    }
}
