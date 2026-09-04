using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Core.Contracts;
using VideoForensics.Providers.Ring;
using VideoForensics.Providers.Common.Contracts;
using VideoForensics.Client.Common;
using VideoForensics.Client.Core;
using VideoForensics.Client.Core.Tools;
using VideoForensics.Hosting;

namespace VideoForensics
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VideoForensics");
            Directory.CreateDirectory(configDir);

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
            var logFilePath = Path.Combine(configDir, "logs", $"videoforensics-{DateTime.Now:yyyy-MM-dd}.log");
            services.AddLogging(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Information);
                builder.AddProvider(new VideoForensics.Logging.FileLoggerProvider(logFilePath, LogLevel.Information));
            });

            // Shared data layer + server-tier provider/orchestrator registrations (session provider,
            // Ring's four services, download/evidence orchestrators, IForensicsConfigurationService,
            // JammingToolsOrchestrator) - see VideoForensics.Hosting/VideoForensicsHostingExtensions.cs.
            services.AddVideoForensicsDataLayer();
            services.AddVideoForensicsServerCore();

            // Register configuration and report rendering services (console-only; not part of the
            // shared server-core registration)
            services.AddSingleton<IForensicReportRenderer>(serviceProvider =>
                new ForensicReportRenderer(
                    serviceProvider.GetRequiredService<IForensicsConfiguration>(),
                    serviceProvider.GetRequiredService<IReportGenerationService>(),
                    serviceProvider.GetRequiredService<ILogger<ForensicReportRenderer>>()
                )
            );

            // Register tool orchestrators for shared use
            services.AddSingleton<VideoForensics.Client.Core.Tools.ConfigToolsOrchestrator>(serviceProvider =>
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
            services.AddSingleton<MenuManager>(serviceProvider =>
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
                    serviceProvider.GetRequiredService<VideoForensics.Client.Core.Tools.JammingToolsOrchestrator>()
                );
            });

            // Build the provider and resolve MenuManager
            var serviceProvider = services.BuildServiceProvider();

            var initLogger = serviceProvider.GetRequiredService<ILogger<Program>>();

            // Surface raw Ring API traffic (never auth bodies/tokens - see ApiRawLogger) to the
            // file log, so device-discovery/API issues can be diagnosed from what Ring actually
            // returned instead of guessing.
            var apiLogger = serviceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("RingApi");
            VideoForensics.Providers.Ring.ApiRawLogger.OnRawResponse += call =>
                apiLogger.LogInformation("{Method} {Url} -> {StatusCode}: {Body}", call.Method, call.Url, call.StatusCode, call.Body);
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

            var menuManager = serviceProvider.GetRequiredService<MenuManager>();

            // Show UI
            await menuManager.ShowMainMenuAsync();
        }
    }
}
