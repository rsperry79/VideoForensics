/// <summary>
/// VideoForensics MCP Server entry point. Initializes 4-phase forensic analysis pipeline.
///
/// EXTERNAL DOCUMENTATION:
/// - E2E testing guide: see _docs_external/E2E_TESTING_GUIDE.md
/// - Claude Desktop setup: see _docs_external/README_CLAUDE_DESKTOP.md
/// - Main README: see README.md in this directory
/// </summary>

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
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

            // Build host with full DI setup
            var builder = Host.CreateApplicationBuilder(args);
            builder.Logging.SetMinimumLevel(LogLevel.Information);

            // Shared session provider
            builder.Services.AddSingleton<ISessionProvider, SessionProvider>();
            builder.Services.AddSingleton<ICredentialStore>(new CredentialStore());

            // Ring provider services
            builder.Services.AddSingleton<IProviderAuthService>(provider =>
                new RingAuthService(
                    provider.GetRequiredService<ILogger<RingAuthService>>(),
                    provider.GetRequiredService<ISessionProvider>(),
                    provider.GetRequiredService<ICredentialStore>(),
                    provider.GetRequiredService<ICredentialRepository>(),
                    provider.GetRequiredService<IRingAccountRepository>(),
                    provider.GetRequiredService<IProviderAccountRepository>(),
                    provider.GetRequiredService<IUserRepository>()
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

            // Data layer
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

            // MCP Tool classes (Phases 1-4)
            builder.Services.AddScoped<VideoForensics.Mcp.Tools.TimelineTools>();
            builder.Services.AddScoped<VideoForensics.Mcp.Tools.IntegrityTools>();
            builder.Services.AddScoped<VideoForensics.Mcp.Tools.CorrelationTools>();
            builder.Services.AddScoped<VideoForensics.Mcp.Tools.AuditTrailTools>();

            // MCP server: stdio transport, attribute-discovered tools/resources
            builder.Services
                .AddMcpServer()
                .WithStdioServerTransport()
                .WithToolsFromAssembly()
                .WithResourcesFromAssembly();

            using var host = builder.Build();
            var initLogger = host.Services.GetRequiredService<ILogger<Program>>();

            try
            {
                initLogger.LogInformation("Host built successfully. Deferring database initialization...");
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"FATAL: Failed to get logger: {ex}");
                return;
            }

            // LAZY: Initialize database in background without blocking MCP startup
            var dbFactory = host.Services.GetRequiredService<IDbContextFactory<VideoForensicsDbContext>>();
            var dbInitTask = DatabaseInitializer.InitializeAsync(dbFactory, initLogger, CancellationToken.None)
                .ContinueWith(t =>
                {
                    if (t.IsFaulted)
                    {
                        initLogger.LogCritical(t.Exception, "Deferred database initialization failed.");
                    }
                    else
                    {
                        initLogger.LogInformation("Deferred database initialization completed.");
                    }
                }, TaskScheduler.Default);

            initLogger.LogInformation("Database initialization started in background. MCP server can respond to requests immediately.");

            // LAZY, ONE-TIME: backfill the Events table from existing DownloadEvents/MediaItems history.
            // Events (independent of download status) is what all forensic Timeline/Integrity/Correlation/
            // Audit tools read from, but historically nothing wrote to it. Runs once after DB init, gated
            // by an AppSettings flag so subsequent startups skip it.
            var eventsBackfillTask = dbInitTask.ContinueWith(async _ =>
            {
                const string backfillFlagKey = "EventsBackfillFromDownloadEventsCompleted";
                try
                {
                    var appSettingRepo = host.Services.GetRequiredService<IAppSettingRepository>();
                    var alreadyDone = await appSettingRepo.GetAsync(backfillFlagKey, CancellationToken.None);
                    if (alreadyDone == "true")
                    {
                        initLogger.LogInformation("Events backfill already completed previously, skipping.");
                        return;
                    }

                    var downloadEventRepo = host.Services.GetRequiredService<IDownloadEventRepository>();
                    var mediaItemRepo = host.Services.GetRequiredService<IMediaItemRepository>();
                    var eventRepo = host.Services.GetRequiredService<IEventRepository>();

                    initLogger.LogInformation("Running one-time Events backfill from existing DownloadEvents...");
                    var count = await EventBackfillService.BackfillFromDownloadEventsAsync(
                        downloadEventRepo, mediaItemRepo, eventRepo, initLogger, CancellationToken.None);
                    await appSettingRepo.SetAsync(backfillFlagKey, "true", CancellationToken.None);
                    initLogger.LogInformation("Events backfill completed: {Count} record(s).", count);
                }
                catch (Exception ex)
                {
                    initLogger.LogError(ex, "Events backfill failed.");
                }
            }, TaskScheduler.Default).Unwrap();

            // LAZY: Load persisted settings in background without blocking MCP startup
            var configLoadTask = Task.Run(async () =>
            {
                try
                {
                    initLogger.LogInformation("Loading persisted settings from database (background)...");
                    var configService = host.Services.GetRequiredService<IForensicsConfigurationService>();
                    var appConfig = host.Services.GetRequiredService<IForensicsConfiguration>() as ForensicsConfiguration
                        ?? throw new InvalidOperationException("Configuration must be ForensicsConfiguration instance");
                    var configLogger = host.Services.GetRequiredService<ILogger<Program>>();
                    await ConfigurationLoader.LoadAndApplyAsync(configService, appConfig, configLogger, CancellationToken.None);
                    initLogger.LogInformation("Configuration loaded successfully (background).");
                }
                catch (Exception ex)
                {
                    initLogger.LogError(ex, "Configuration loading failed in background.");
                }
            });

            initLogger.LogInformation("Configuration loading started in background.");

            initLogger.LogInformation("VideoForensics MCP Server ready.");
            initLogger.LogInformation("All 4 forensics query phases initialized with optimization:");
            initLogger.LogInformation("  ✓ Phase 1: Timeline & Patterns (8 methods + summary + pagination)");
            initLogger.LogInformation("  ✓ Phase 2: Evidence Integrity (10 methods + summary + pagination)");
            initLogger.LogInformation("  ✓ Phase 3: Correlation Queries (7 methods + summary + pagination)");
            initLogger.LogInformation("  ✓ Phase 4: Access & Export Audit (9 methods + summary + pagination)");
            initLogger.LogInformation("");
            initLogger.LogInformation("OPTIMIZATIONS ENABLED:");
            initLogger.LogInformation("  • Summary + Detail-on-Demand: Fast decisions via lightweight summaries");
            initLogger.LogInformation("  • Pagination: Offset-based (PaginatedResult) and cursor-based (CursorPaginatedResult)");
            initLogger.LogInformation("  • Parallel Queries: All 4 phases can be called simultaneously without blocking");
            initLogger.LogInformation("  • Streaming Ready: Cursor-paginated results support incremental data flow");
            initLogger.LogInformation("");
            initLogger.LogInformation("PARALLEL QUERY PATTERN:");
            initLogger.LogInformation("  await Task.WhenAll(");
            initLogger.LogInformation("    timelineRepo.GetTimelineSummaryAsync(...),");
            initLogger.LogInformation("    integrityRepo.GetIntegritySummaryAsync(...),");
            initLogger.LogInformation("    correlationRepo.GetCorrelationSummaryAsync(...),");
            initLogger.LogInformation("    auditRepo.GetAuditTrailSummaryAsync(...)");
            initLogger.LogInformation("  )");

            // Note: Database optimization via PRAGMA optimize; is handled by DatabaseInitializer during migrations.
            // Diagnostics-based maintenance has been deferred to avoid blocking MCP initialization.

            try
            {
                initLogger.LogInformation("Initializing MCP server with attribute-based tool/resource discovery...");
                // MCP server uses attribute-based discovery for tools and resources
                // All classes marked with [McpServerToolType] or [McpServerResourceType] are auto-registered
                // The framework handles stdio transport and lifecycle automatically
                initLogger.LogInformation("MCP Server configured and ready");
            }
            catch (Exception ex)
            {
                initLogger.LogError(ex, "FATAL: MCP server configuration failed");
                Console.Error.WriteLine($"MCP CONFIG ERROR: {ex}");
                throw;
            }
            initLogger.LogInformation("All tool classes (Timeline, Integrity, Correlation, Audit) configured with [McpServerToolType]");
            initLogger.LogInformation("Resource: jamming-analysis-instructions configured with [McpServerResourceType]");
            initLogger.LogInformation("");
            initLogger.LogInformation("MCP server runs on stdio transport when invoked by Claude Desktop via claude_desktop_config.json");
            initLogger.LogInformation("Configured tools use dependency injection to access repositories and loggers");
            initLogger.LogInformation("=== MCP SERVER READY FOR CONNECTIONS ===");

            try
            {
                // Runs all hosted services, including the MCP stdio transport, until shutdown
                await host.RunAsync();
            }
            catch (Exception ex)
            {
                initLogger.LogError(ex, "FATAL: Unexpected error in main loop");
                Console.Error.WriteLine($"FATAL MAIN LOOP ERROR: {ex}");
                throw;
            }
        }
    }
}
