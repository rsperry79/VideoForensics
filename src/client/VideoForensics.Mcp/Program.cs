using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.SDK;
using ModelContextProtocol.SDK.Stdio;
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
            using var host = Host.CreateApplicationBuilder(args)
                .AddLogging(builder => builder.SetMinimumLevel(LogLevel.Information))
                .ConfigureServices((context, services) =>
                {
                    // Register MCP server with stdio transport
                    services.AddMcpServer(mcpBuilder =>
                    {
                        mcpBuilder
                            .WithStdioServerTransport()
                            .WithToolsFromAssembly()
                            .WithResourcesFromAssembly();
                    });

                    // Shared session provider
                    services.AddSingleton<ISessionProvider, SessionProvider>();
                    services.AddSingleton<ICredentialStore>(new CredentialStore());

                    // Ring provider services
                    services.AddSingleton<IProviderAuthService>(provider =>
                        new RingAuthService(
                            provider.GetRequiredService<ILogger<RingAuthService>>(),
                            provider.GetRequiredService<ISessionProvider>(),
                            provider.GetRequiredService<ICredentialStore>()
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

                    // RingVideoProvider
                    services.AddSingleton<IVideoProvider>(provider =>
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
                    services.AddSingleton<IForensicsConfiguration>(runtimeConfig);

                    // Data layer (SQLite, EF Core, repositories, Data.Core)
                    services.AddVideoForensicsSqlite();
                    services.AddVideoForensicsDatabase();
                    services.AddVideoForensicsDataCore();

                    // Video download service
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

                    // Evidence validation service
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

                    // Evidence export service
                    services.AddSingleton<IEvidenceExportService>(serviceProvider =>
                    {
                        var logger = serviceProvider.GetRequiredService<ILogger<EvidenceExportOrchestrator>>();
                        var mediaItemRepository = serviceProvider.GetRequiredService<IMediaItemRepository>();
                        var integrityVerificationService = serviceProvider.GetRequiredService<IIntegrityVerificationService>();
                        var actionLogRepository = serviceProvider.GetRequiredService<IActionLogRepository>();
                        var exportRecordService = serviceProvider.GetRequiredService<IExportRecordService>();
                        return new EvidenceExportOrchestrator(logger, mediaItemRepository, integrityVerificationService, actionLogRepository, exportRecordService);
                    });

                    // Forensics configuration service
                    services.AddSingleton<IForensicsConfigurationService>(serviceProvider =>
                        new ForensicsConfigurationService(
                            serviceProvider.GetRequiredService<ILogger<ForensicsConfigurationService>>(),
                            serviceProvider.GetRequiredService<IAppSettingRepository>()
                        )
                    );

                    // Forensics query repositories (Phases 1-4)
                    services.AddScoped<ITimelineRepository, TimelineRepository>();
                    services.AddScoped<IIntegrityRepository, IntegrityRepository>();
                    services.AddScoped<ICorrelationRepository, CorrelationRepository>();
                    services.AddScoped<IAuditTrailRepository, AuditTrailRepository>();
                })
                .Build();

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
            var runtimeConfig = host.Services.GetRequiredService<IForensicsConfiguration>();
            var configLogger = host.Services.GetRequiredService<ILogger<Program>>();
            await ConfigurationLoader.LoadAndApplyAsync(configService, runtimeConfig, configLogger, CancellationToken.None);

            // Run MCP server
            var mcpServer = host.Services.GetRequiredService<IMcpServer>();
            await mcpServer.RunAsync();
        }
    }
}
