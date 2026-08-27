using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Core.Contracts;
using VideoForensics.Data.Core.DependencyInjection;
using VideoForensics.Data.Database.DbContext;
using VideoForensics.Data.Database.DependencyInjection;
using VideoForensics.Data.Database.Sqlite.DependencyInjection;
using VideoForensics.Data.Database.Sqlite.Migrations;
using VideoForensics.Providers.Ring;
using VideoForensics.Providers.Ring.Services;
using VideoForensics.Providers.Common.Contracts;
using VideoForensics.Client.Common;
using VideoForensics.Client.Core;

namespace VideoForensics
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VideoForensics");
            Directory.CreateDirectory(configDir);
            var configPath = Path.Combine(configDir, "ForensicsConfig.json");
            var credentialPath = Path.Combine(configDir, "RingCredentials.json");

            // Run demo mode if launched with --demo flag
            if (args.Length > 0 && args[0] == "--demo")
            {
                DemoMode.RunDemo();
                return;
            }

            // Build dependency injection container
            var services = new ServiceCollection();

            // Register logging
            services.AddLogging(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Information);
            });

            // Register the shared session provider (must be singleton so all services observe same session)
            services.AddSingleton<ISessionProvider, SessionProvider>();

            // Register credential store for persisting auth tokens
            services.AddSingleton<ICredentialStore>(new CredentialStore());

            // Register Ring provider services with factories that provide typed loggers
            services.AddSingleton<IProviderAuthService>(provider =>
                new RingAuthService(
                    provider.GetRequiredService<ILogger<RingAuthService>>(),
                    provider.GetRequiredService<ISessionProvider>(),
                    provider.GetRequiredService<ICredentialStore>(),
                    credentialPath
                )
            );
            services.AddSingleton<IDeviceDiscoveryService>(provider =>
                new RingDeviceDiscoveryService(
                    provider.GetRequiredService<ILogger<RingDeviceDiscoveryService>>(),
                    provider.GetRequiredService<ISessionProvider>()
                )
            );
            services.AddSingleton<IMediaDownloadService>(provider =>
                new RingMediaDownloadService(
                    provider.GetRequiredService<ILogger<RingMediaDownloadService>>(),
                    provider.GetRequiredService<ISessionProvider>(),
                    provider.GetRequiredService<IVideoForensicsDataClient>()
                )
            );
            services.AddSingleton<IEventAndConfigService>(provider =>
                new RingEventAndConfigService(
                    provider.GetRequiredService<ILogger<RingEventAndConfigService>>(),
                    provider.GetRequiredService<ISessionProvider>()
                )
            );

            // Register RingVideoProvider as factory using injected services
            services.AddSingleton<IVideoProvider>(provider =>
                new RingVideoProvider(
                    provider.GetRequiredService<ILogger<RingVideoProvider>>(),
                    provider.GetRequiredService<IProviderAuthService>(),
                    provider.GetRequiredService<IDeviceDiscoveryService>(),
                    provider.GetRequiredService<IMediaDownloadService>(),
                    provider.GetRequiredService<IEventAndConfigService>()
                )
            );

            // Register local console app configuration and services. Settings are stored in the
            // database (see IForensicsConfigurationService below), but the DI container isn't built
            // yet and the database hasn't been migrated at this point, so this singleton starts out
            // holding just the class defaults. It's populated from the database further down, once
            // the container is built and DatabaseInitializer has run — mutating this same instance in
            // place so every service already holding a reference to it (MenuManager, ForensicReportRenderer,
            // etc.) observes the loaded values too.
            var runtimeConfig = new ForensicsConfiguration();
            services.AddSingleton<IForensicsConfiguration>(runtimeConfig);
            // Register data layer services (SQLite, EF Core, repositories, and Data.Core facade)
            // Registered before IVideoDownloadService below since VideoDownloadServiceAdapter now
            // depends on IVideoForensicsDataClient for watermark resolution.
            services.AddVideoForensicsSqlite();
            services.AddVideoForensicsDatabase();
            services.AddVideoForensicsDataCore();

            services.AddSingleton<IVideoDownloadService>(serviceProvider =>
            {
                var logger = serviceProvider.GetRequiredService<ILogger<VideoDownloadServiceAdapter>>();
                var videoProvider = serviceProvider.GetRequiredService<IVideoProvider>();
                var authService = serviceProvider.GetRequiredService<IProviderAuthService>();
                var downloadService = serviceProvider.GetRequiredService<IMediaDownloadService>();
                var deviceService = serviceProvider.GetRequiredService<IDeviceDiscoveryService>();
                var dataClient = serviceProvider.GetRequiredService<IVideoForensicsDataClient>();
                var forensicsConfig = serviceProvider.GetRequiredService<IForensicsConfiguration>();
                return new VideoDownloadServiceAdapter(logger, videoProvider, authService, downloadService, deviceService, dataClient, forensicsConfig);
            });

            services.AddSingleton<IEvidenceValidationService>(serviceProvider =>
            {
                var logger = serviceProvider.GetRequiredService<ILogger<EvidenceValidationOrchestrator>>();
                var eventAndConfigService = serviceProvider.GetRequiredService<IEventAndConfigService>();
                var eventRepository = serviceProvider.GetRequiredService<IEventRepository>();
                var deviceRepository = serviceProvider.GetRequiredService<IDeviceRepository>();
                var integrityService = serviceProvider.GetRequiredService<IIntegrityVerificationService>();
                var mediaItemRepository = serviceProvider.GetRequiredService<IMediaItemRepository>();
                var reconciliationService = serviceProvider.GetRequiredService<IProviderReconciliationService>();
                return new EvidenceValidationOrchestrator(logger, eventAndConfigService, eventRepository, deviceRepository, integrityService, mediaItemRepository, reconciliationService);
            });

            services.AddSingleton<IEvidenceExportService>(serviceProvider =>
            {
                var logger = serviceProvider.GetRequiredService<ILogger<EvidenceExportOrchestrator>>();
                var mediaItemRepository = serviceProvider.GetRequiredService<IMediaItemRepository>();
                var integrityVerificationService = serviceProvider.GetRequiredService<IIntegrityVerificationService>();
                var actionLogRepository = serviceProvider.GetRequiredService<IActionLogRepository>();
                var exportRecordService = serviceProvider.GetRequiredService<IExportRecordService>();
                return new EvidenceExportOrchestrator(logger, mediaItemRepository, integrityVerificationService, actionLogRepository, exportRecordService);
            });

            // Register configuration and report rendering services
            services.AddSingleton<IForensicsConfigurationService>(serviceProvider =>
                new ForensicsConfigurationService(
                    serviceProvider.GetRequiredService<ILogger<ForensicsConfigurationService>>(),
                    configPath,
                    serviceProvider.GetRequiredService<IAppSettingRepository>()
                )
            );
            services.AddSingleton<IForensicReportRenderer>(serviceProvider =>
                new ForensicReportRenderer(
                    serviceProvider.GetRequiredService<IForensicsConfiguration>(),
                    serviceProvider.GetRequiredService<IReportGenerationService>(),
                    serviceProvider.GetRequiredService<ILogger<ForensicReportRenderer>>()
                )
            );

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
                    serviceProvider.GetRequiredService<IVideoForensicsDataClient>(),
                    serviceProvider.GetRequiredService<IEventRepository>(),
                    serviceProvider.GetRequiredService<IDeviceConfigRepository>(),
                    serviceProvider.GetRequiredService<IDeviceRepository>(),
                    serviceProvider.GetRequiredService<IMediaItemRepository>(),
                    serviceProvider.GetRequiredService<IEvidenceValidationService>(),
                    serviceProvider.GetRequiredService<IEvidenceExportService>(),
                    serviceProvider.GetRequiredService<IAppSettingRepository>()
                );
            });

            // Build the provider and resolve MenuManager
            var serviceProvider = services.BuildServiceProvider();

            // Initialize database with migrations and integrity checks
            var dbFactory = serviceProvider.GetRequiredService<IDbContextFactory<VideoForensicsDbContext>>();
            var initLogger = serviceProvider.GetRequiredService<ILogger<Program>>();
            try
            {
                await DatabaseInitializer.InitializeAsync(dbFactory, initLogger, CancellationToken.None);
            }
            catch (Exception ex)
            {
                initLogger.LogCritical(ex, "Database initialization failed. The application cannot continue.");
                AnsiConsole.MarkupLine("[red]✗ Database initialization failed: {0}[/]", ex.Message.Replace("[", "[[").Replace("]", "]]"));
                return;
            }

            // Load persisted settings from the database now that the container is built and the
            // database is migrated, and apply them onto the already-registered IForensicsConfiguration
            // singleton (see registration above for why this can't just replace it outright).
            var configService = serviceProvider.GetRequiredService<IForensicsConfigurationService>();
            var loadedConfig = await configService.LoadConfigurationAsync(configPath);
            runtimeConfig.EnableForensicAnalysisReports = loadedConfig.EnableForensicAnalysisReports;
            runtimeConfig.EnableSignalAnomalyReports = loadedConfig.EnableSignalAnomalyReports;
            runtimeConfig.EnableChainOfCustodyReports = loadedConfig.EnableChainOfCustodyReports;
            runtimeConfig.EnableEvidenceValidationReports = loadedConfig.EnableEvidenceValidationReports;
            runtimeConfig.EnableAccessControlMonitoring = loadedConfig.EnableAccessControlMonitoring;
            runtimeConfig.EnablePiiRedaction = loadedConfig.EnablePiiRedaction;
            runtimeConfig.ReportsDirectory = loadedConfig.ReportsDirectory;
            runtimeConfig.ReportOutputFormat = loadedConfig.ReportOutputFormat;
            runtimeConfig.DownloadLocation = loadedConfig.DownloadLocation;
            runtimeConfig.RedactionLevel = loadedConfig.RedactionLevel;
            runtimeConfig.KeyStorageProvider = loadedConfig.KeyStorageProvider;
            runtimeConfig.RetentionDaysDefault = loadedConfig.RetentionDaysDefault;
            runtimeConfig.LogLevel = loadedConfig.LogLevel;

            // Migrate legacy credentials from old JSON file to encrypted credential store
            var legacyMigrator = new LegacyCredentialMigrator(
                serviceProvider.GetRequiredService<ILogger<LegacyCredentialMigrator>>(),
                serviceProvider.GetRequiredService<IVideoForensicsDataClient>(),
                credentialPath);
            await legacyMigrator.MigrateIfNeededAsync(CancellationToken.None);

            // Try to restore from saved credentials (skip if no saved credentials)
            var authService = serviceProvider.GetRequiredService<IProviderAuthService>();
            await authService.RestoreFromSavedCredentialsAsync();

            var menuManager = serviceProvider.GetRequiredService<MenuManager>();

            // Show UI
            await menuManager.ShowMainMenuAsync();
        }
    }
}
