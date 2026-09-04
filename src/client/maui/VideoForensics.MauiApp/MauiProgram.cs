using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VideoForensics.Hosting;

namespace VideoForensics.MauiApp
{
    public static class MauiProgram
    {
        public static Microsoft.Maui.Hosting.MauiApp CreateMauiApp()
        {
            var builder = Microsoft.Maui.Hosting.MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                });

            builder.Services.AddMauiBlazorWebView();

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
