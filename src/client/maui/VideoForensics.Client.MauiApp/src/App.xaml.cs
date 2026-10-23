using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.MauiApp.AppLock;

namespace VideoForensics.Client.MauiApp;

public partial class App : Application
{
    private readonly IServiceProvider _services;
    private DateTime? _backgroundedAtUtc;

    public App(IServiceProvider services)
    {
        StartupTrace.Mark("App constructor: start");
        InitializeComponent();
        _services = services;
        StartupTrace.Mark("App constructor: done");
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        StartupTrace.Mark("CreateWindow: start");
        var window = new Window { Title = "VideoForensics" };
        window.Page = BuildLockPage(window);
        StartupTrace.Mark("CreateWindow: lock page set");

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

            TimeSpan elapsed = DateTime.UtcNow - _backgroundedAtUtc.Value;
            TimeSpan timeout = _services.GetRequiredService<IAppLockPreferencesStore>().GetIdleLockTimeout();
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
        ILocalAuthGate authGate = _services.GetRequiredService<ILocalAuthGate>();
        AppLockPage lockPage = new(authGate);
        lockPage.Unlocked += (_, _) => window.Page = new MainPage();
        return lockPage;
    }
}
