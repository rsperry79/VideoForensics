using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Spectre.Console;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Core.Logging.DependencyInjection;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Core.Contracts;
using VideoForensics.Hosting;
using VideoForensics.Providers.Common.Contracts;
using VideoForensics.Providers.Common.Helpers.Platform;

namespace VideoForensics
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            string configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "VideoForensics");
            _ = Directory.CreateDirectory(configDir);

            // Run demo mode if launched with --demo flag
            if (args.Length > 0 && args[0] == "--demo")
            {
                DemoMode.RunDemo();
                return;
            }

            // Build dependency injection container
            var services = new ServiceCollection();

            // Register logging. Writes to a file rather than the console since this is an
            // interactive Spectre.Console TUI - console logging would corrupt the menu rendering.
            string logFilePath = Path.Combine(configDir, "logs", $"videoforensics-{DateTime.Now:yyyy-MM-dd}.log");
            _ = services.AddLogging(builder =>
            {
                _ = builder.SetMinimumLevel(LogLevel.Information);
                _ = builder.AddVideoForensicsLogging(logFilePath, LogLevel.Information);
            });

            // Session tokens, step-up tokens, and encrypted credentials (stored by RingAuthService,
            // RingDeviceDiscoveryService, etc.) are all IDataProtector-protected. AddDataProtection()
            // alone relies on ASP.NET Core's default key-storage heuristic (usually %LOCALAPPDATA%\ASP.NET\
            // DataProtection-Keys on Windows) - explicit here so a credential encrypted by one host
            // (WebApp, MAUI, this console app) can be decrypted by another instead of depending on
            // that heuristic continuing to resolve the same way. Keys live next to this app's database
            // and logs (%ProgramData%\VideoForensics\keys), matching how every other persistent state
            // is already rooted at %ProgramData%\VideoForensics.
            string dataProtectionKeyPath = Path.Combine(configDir, "keys");
            _ = Directory.CreateDirectory(dataProtectionKeyPath);
            IDataProtectionBuilder dataProtectionBuilder = services.AddDataProtection()
                .SetApplicationName("VideoForensics")
                .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeyPath));
            // DPAPI encrypts the key-ring file at rest but is Windows-only (this app is platform-agnostic).
            // On Linux/macOS the key-ring falls back to ASP.NET Core's unencrypted-on-disk default,
            // protected only by filesystem permissions on the ProgramData-equivalent directory above.
            if (OperatingSystem.IsWindows())
            {
                _ = dataProtectionBuilder.ProtectKeysWithDpapi();
            }

            // Shared data layer + server-tier provider/orchestrator registrations (session provider,
            // active provider's four services, download/evidence orchestrators, IForensicsConfigurationService,
            // JammingToolsOrchestrator) - see VideoForensics.Hosting/VideoForensicsHostingExtensions.cs.
            // The active provider is read from the VIDEOFORENSICS_ActiveProvider environment variable,
            // defaulting to "Ring" for backward compatibility.
            string activeProvider = Environment.GetEnvironmentVariable("VIDEOFORENSICS_ActiveProvider") ?? "Ring";
            _ = services.AddVideoForensicsDataLayer();
            _ = services.AddVideoForensicsServerCore(activeProvider);

            // Register configuration and report rendering services (console-only; not part of the
            // shared server-core registration)
            _ = services.AddSingleton<IForensicReportRenderer>(serviceProvider =>
                new ForensicReportRenderer(
                    serviceProvider.GetRequiredService<IForensicsConfiguration>(),
                    serviceProvider.GetRequiredService<IReportGenerationService>(),
                    serviceProvider.GetRequiredService<ILogger<ForensicReportRenderer>>()
                )
            );

            // Register tool orchestrators for shared use
            _ = services.AddSingleton<VideoForensics.Client.Core.Tools.ConfigToolsOrchestrator>(serviceProvider =>
                new VideoForensics.Client.Core.Tools.ConfigToolsOrchestrator(
                    serviceProvider.GetRequiredService<ILogger<VideoForensics.Client.Core.Tools.ConfigToolsOrchestrator>>(),
                    serviceProvider.GetRequiredService<IForensicsConfigurationService>(),
                    serviceProvider.GetRequiredService<IAppSettingRepository>(),
                    serviceProvider.GetRequiredService<IDatabaseMaintenanceService>()
                )
            );

            // Note: JammingToolsOrchestrator is registered by AddVideoForensicsServerCore() above
            // (as Scoped, not Singleton like before - this single-scope console host never creates
            // child scopes, so it behaves identically to Singleton here).

            // Register MenuManager with injected dependencies
            _ = services.AddSingleton<MenuManager>(serviceProvider =>
            {
                return new MenuManager(
                    serviceProvider.GetRequiredService<ILogger<MenuManager>>(),
                    serviceProvider.GetRequiredService<IForensicsConfiguration>(),
                    serviceProvider.GetRequiredService<IForensicsConfigurationService>(),
                    serviceProvider.GetRequiredService<IVideoDownloadService>(),
                    serviceProvider.GetRequiredService<IForensicReportRenderer>(),
                    serviceProvider.GetRequiredService<IProviderAuthService>(),
                    serviceProvider.GetRequiredService<IDeviceDiscoveryService>(),
                    serviceProvider.GetRequiredService<IEventAndConfigService>(),
                    serviceProvider.GetRequiredService<IVideoForensicsDataClient>(),
                    serviceProvider.GetRequiredService<IEventRepository>(),
                    serviceProvider.GetRequiredService<IDeviceConfigRepository>(),
                    serviceProvider.GetRequiredService<IDeviceRepository>(),
                    serviceProvider.GetRequiredService<IMediaItemRepository>(),
                    serviceProvider.GetRequiredService<IEvidenceValidationService>(),
                    serviceProvider.GetRequiredService<IEvidenceExportService>(),
                    serviceProvider.GetRequiredService<IAppSettingRepository>(),
                    serviceProvider.GetRequiredService<IProviderAccountRepository>(),
                    serviceProvider.GetRequiredService<IUserRepository>(),
                    serviceProvider.GetRequiredService<VideoForensics.Client.Core.Tools.ConfigToolsOrchestrator>(),
                    serviceProvider.GetRequiredService<VideoForensics.Client.Core.Tools.JammingToolsOrchestrator>(),
                    serviceProvider.GetRequiredService<IStorageLocationProvider>()
                );
            });

            // Build the provider and resolve MenuManager
            ServiceProvider serviceProvider = services.BuildServiceProvider();

            ILogger<Program> initLogger = serviceProvider.GetRequiredService<ILogger<Program>>();

            // Surface raw Ring API traffic (never auth bodies/tokens - see ApiRawLogger) to the
            // file log, so device-discovery/API issues can be diagnosed from what Ring actually
            // returned instead of guessing.
            ILogger apiLogger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("RingApi");
            VideoForensics.Providers.Ring.ApiRawLogger.OnRawResponse += call =>
                apiLogger.LogInformation("{Method} {Url} -> {StatusCode}: {Body}", SanitizeForLog(call.Method), SanitizeForLog(call.Url), call.StatusCode, SanitizeForLog(call.Body));
            VideoForensics.Providers.Ring.ApiRawLogger.OnEvent += evt =>
                apiLogger.LogInformation("[{Category}] {Message}", evt.Category, evt.Message);

            // DB init + Events backfill + persisted-config load, in that order - see
            // VideoForensicsHostingExtensions.InitializeVideoForensicsDataAsync.
            try
            {
                await VideoForensicsHostingExtensions.InitializeVideoForensicsDataAsync(serviceProvider, initLogger, CancellationToken.None);
            }
            catch (Exception ex)
            {
                initLogger.LogCritical(ex, "Database initialization failed. The application cannot continue.");
                AnsiConsole.MarkupLine("[red]✗ Database initialization failed: {0}[/]", ex.Message.Replace("[", "[[").Replace("]", "]]"));
                return;
            }

            MenuManager menuManager = serviceProvider.GetRequiredService<MenuManager>();

            // Show UI
            await menuManager.ShowMainMenuAsync();
        }

        /// <summary>Sanitizes a string for logging by replacing newline characters to prevent log forging.</summary>
        private static string? SanitizeForLog(string? value) =>
            string.IsNullOrEmpty(value) ? value : value.Replace('\r', '_').Replace('\n', '_');
    }
}
