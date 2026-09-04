using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Maui;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;
using VideoForensics.Client.Common;
using VideoForensics.Hosting;
using VideoForensics.MauiApp.AppLock;

namespace VideoForensics.MauiApp
{
    public static class MauiProgram
    {
        public static Microsoft.Maui.Hosting.MauiApp CreateMauiApp()
        {
            var builder = Microsoft.Maui.Hosting.MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                });

            builder.Services.AddMauiBlazorWebView();
            builder.Services.AddMudServices();

            // Register file-based logging - there's no console to log to in a MAUI app. Log file
            // lands under %AppData%/VideoForensics/logs, matching the console app's pattern
            // (src/client/VideoForensics/Program.cs).
            var configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VideoForensics");
            Directory.CreateDirectory(configDir);
            var logFilePath = Path.Combine(configDir, "logs", $"videoforensics-maui-{DateTime.Now:yyyy-MM-dd}.log");
            builder.Logging.SetMinimumLevel(LogLevel.Information);
            builder.Logging.AddProvider(new VideoForensics.MauiApp.Logging.FileLoggerProvider(logFilePath, LogLevel.Information));

            // Shared data layer + server-tier provider/orchestrator registrations (session provider,
            // Ring's four services, download/evidence orchestrators, JammingToolsOrchestrator) - see
            // VideoForensics.Hosting/VideoForensicsHostingExtensions.cs. This is a deliberate,
            // temporary bootstrap shape for this milestone: MAUI talks to the Ring provider directly
            // and owns its own local SQLite DB, same as console/MCP today. The long-term plan has
            // MAUI as a thin client that only talks to a future server-owned API surface - that split
            // (Minimal API + HTTP-based remote repositories) is separately scoped, later work.
            builder.Services.AddVideoForensicsDataLayer();
            builder.Services.AddVideoForensicsServerCore();

            // Local app-lock (plan §5.9) - overrides the no-op IAppLockPreferencesStore default that
            // AddVideoForensicsDataLayer() just registered for every host. Windows-only for now,
            // matching this MAUI target's own scope.
            builder.Services.AddSingleton<ILocalAuthGate, FingerprintLocalAuthGate>();
            builder.Services.AddSingleton<IAppLockPreferencesStore, MauiAppLockPreferencesStore>();

            // NOT calling AddVideoForensicsClientApi() here (yet): it's built and proven working
            // (M5 - see VideoForensicsHostingExtensions.AddVideoForensicsClientApi and the Remote/
            // folder), but registering it alongside AddVideoForensicsServerCore() above would give
            // IDeviceRepository/IMediaItemRepository/IIntegrityRecordRepository two competing
            // registrations - DI resolves the LAST one registered, so it would silently swap
            // MauiApp's currently-working local-SQLite-backed sign-in/dashboard over to hitting a
            // hardcoded http://localhost:5162 that may not be running, breaking it outright. MauiApp
            // switches to AddVideoForensicsClientApi() (replacing, not joining, the two calls above)
            // once M6's QR pairing gives it a real server address and a reason to stop talking to
            // Ring directly - until then this stays the same local-dev bootstrap M1 already used.

#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
            builder.Logging.AddDebug();
#endif

            var app = builder.Build();

            // LAZY: DB init + Events backfill + persisted-config load, without blocking MAUI startup
            // (MAUI apps don't have an async Main the way console apps do) - mirrors the
            // fire-and-forget pattern in VideoForensics.Mcp/Program.cs.
            var initLogger = app.Services.GetRequiredService<ILogger<Microsoft.Maui.Hosting.MauiApp>>();
            _ = Task.Run(async () =>
            {
                try
                {
                    await VideoForensicsHostingExtensions.InitializeVideoForensicsDataAsync(app.Services, initLogger, CancellationToken.None);
                    initLogger.LogInformation("Deferred initialization (DB, Events backfill, config) completed.");
                }
                catch (Exception ex)
                {
                    initLogger.LogCritical(ex, "Deferred initialization failed.");
                }
            });

            return app;
        }
    }
}
