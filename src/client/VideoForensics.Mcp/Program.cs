using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
// using ModelContextProtocol.SDK;
// using ModelContextProtocol.SDK.Stdio;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Core.Contracts;
using VideoForensics.Data.Core.DependencyInjection;
using VideoForensics.Data.Database.DbContext;
using VideoForensics.Data.Database.DependencyInjection;
using VideoForensics.Data.Database.Repositories;
using VideoForensics.Data.Database.Sqlite.DependencyInjection;
using VideoForensics.Data.Database.Sqlite.Migrations;
using VideoForensics.Providers.Ring;
using VideoForensics.Providers.Ring.Services;
using VideoForensics.Providers.Common.Contracts;
using VideoForensics.Client.Common;
using VideoForensics.Client.Core;
using VideoForensics.Client.Core.Tools;

namespace VideoForensics.Mcp
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var configDir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "VideoForensics");
            Directory.CreateDirectory(configDir);

            // Build host with MCP server
            var builder = Host.CreateApplicationBuilder(args);
            builder.Logging.SetMinimumLevel(LogLevel.Information);

            // TODO: Register MCP server with stdio transport once SDK is properly imported
            // builder.Services.AddMcpServer(mcpBuilder =>
            // {
            //     mcpBuilder
            //         .WithStdioServerTransport()
            //         .WithToolsFromAssembly()
            //         .WithResourcesFromAssembly();
            // });

            // Shared session provider
            builder.Services.AddSingleton<ISessionProvider, SessionProvider>();
            builder.Services.AddSingleton<ICredentialStore>(new CredentialStore());

            // Ring provider services
            builder.Services.AddSingleton<IProviderAuthService>(provider =>
                        new RingAuthService(
                            provider.GetRequiredService<ILogger<RingAuthService>>(),
                            provider.GetRequiredService<ISessionProvider>(),
                            provider.GetRequiredService<ICredentialStore>()
                        )
                    );
            builder.Services.AddSingleton<IDeviceDiscoveryService>(provider =>
                        new RingDeviceDiscoveryService(
                            provider.GetRequiredService<ILogger<RingDeviceDiscoveryService>>(),
                            provider.GetRequiredService<ISessionProvider>()
                        )
                    );
            builder.Services.AddSingleton<IMediaDownloadService>(provider =>
                        new RingMediaDownloadService(
                            provider.GetRequiredService<ILogger<RingMediaDownloadService>>(),
                            provider.GetRequiredService<ISessionProvider>(),
                            provider.GetRequiredService<IVideoForensicsDataClient>()
                        )
                    );
            builder.Services.AddSingleton<IEventAndConfigService>(provider =>
                        new RingEventAndConfigService(
                            provider.GetRequiredService<ILogger<RingEventAndConfigService>>(),
                            provider.GetRequiredService<ISessionProvider>()
                        )
                    );

                    // RingVideoProvider
            builder.Services.AddSingleton<IVideoProvider>(provider =>
                        new RingVideoProvider(
                            provider.GetRequiredService<ILogger<RingVideoProvider>>(),
                            provider.GetRequiredService<IProviderAuthService>(),
                            provider.GetRequiredService<IDeviceDiscoveryService>(),
                            provider.GetRequiredService<IMediaDownloadService>(),
                            provider.GetRequiredService<IEventAndConfigService>()
                        )
                    );

                    // Forensics configuration
                    var runtimeConfig = new ForensicsConfiguration();
            builder.Services.AddSingleton<IForensicsConfiguration>(runtimeConfig);

                    // Data layer (SQLite, EF Core, repositories, Data.Core)
            builder.Services.AddVideoForensicsSqlite();
            builder.Services.AddVideoForensicsDatabase();
            builder.Services.AddVideoForensicsDataCore();

                    // Video download service
            builder.Services.AddSingleton<IVideoDownloadService>(serviceProvider =>
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

                    // Evidence validation service
            builder.Services.AddSingleton<IEvidenceValidationService>(serviceProvider =>
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

                    // Evidence export service
            builder.Services.AddSingleton<IEvidenceExportService>(serviceProvider =>
                    {
                        var logger = serviceProvider.GetRequiredService<ILogger<EvidenceExportOrchestrator>>();
                        var mediaItemRepository = serviceProvider.GetRequiredService<IMediaItemRepository>();
                        var integrityVerificationService = serviceProvider.GetRequiredService<IIntegrityVerificationService>();
                        var actionLogRepository = serviceProvider.GetRequiredService<IActionLogRepository>();
                        var exportRecordService = serviceProvider.GetRequiredService<IExportRecordService>();
                        return new EvidenceExportOrchestrator(logger, mediaItemRepository, integrityVerificationService, actionLogRepository, exportRecordService);
                    });

                    // Forensics configuration service
            builder.Services.AddSingleton<IForensicsConfigurationService>(serviceProvider =>
                        new ForensicsConfigurationService(
                            serviceProvider.GetRequiredService<ILogger<ForensicsConfigurationService>>(),
                            serviceProvider.GetRequiredService<IAppSettingRepository>()
                        )
                    );

                    // Forensics query repositories (Phases 1-4)
            builder.Services.AddScoped<ITimelineRepository, TimelineRepository>();
            builder.Services.AddScoped<IIntegrityRepository, IntegrityRepository>();
            builder.Services.AddScoped<ICorrelationRepository, CorrelationRepository>();
            builder.Services.AddScoped<IAuditTrailRepository, AuditTrailRepository>();

            using var host = builder.Build();

            // Initialize database
            var dbFactory = host.Services.GetRequiredService<IDbContextFactory<VideoForensicsDbContext>>();
            var initLogger = host.Services.GetRequiredService<ILogger<Program>>();
            try
            {
                await DatabaseInitializer.InitializeAsync(dbFactory, initLogger, CancellationToken.None);
            }
            catch (Exception ex)
            {
                initLogger.LogCritical(ex, "Database initialization failed. The MCP server cannot continue.");
                return;
            }

            // Load persisted settings
            var configService = host.Services.GetRequiredService<IForensicsConfigurationService>();
            var appConfig = host.Services.GetRequiredService<IForensicsConfiguration>() as ForensicsConfiguration
                ?? throw new InvalidOperationException("Configuration must be ForensicsConfiguration instance");
            var configLogger = host.Services.GetRequiredService<ILogger<Program>>();
            await ConfigurationLoader.LoadAndApplyAsync(configService, appConfig, configLogger, CancellationToken.None);

            // Run MCP server (TODO: uncomment once MCP SDK is properly configured)
            // var mcpServer = host.Services.GetRequiredService<IMcpServer>();
            // await mcpServer.RunAsync();

            initLogger.LogInformation("MCP server skeleton created. Configure ModelContextProtocol SDK to enable full functionality.");
        }
    }
}
