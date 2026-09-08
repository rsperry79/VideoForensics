/// <summary>
/// VideoForensics MCP Server entry point. Initializes 4-phase forensic analysis pipeline.
///
/// EXTERNAL DOCUMENTATION:
/// - E2E testing guide: see _docs_external/E2E_TESTING_GUIDE.md
/// - Claude Desktop setup: see _docs_external/README_CLAUDE_DESKTOP.md
/// - Main README: see README.md in this directory
/// </summary>

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Database.Repositories;
using VideoForensics.Hosting;

namespace VideoForensics.Mcp
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            string configDir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "VideoForensics");
            _ = Directory.CreateDirectory(configDir);

            // Build host with full DI setup
            var builder = Host.CreateApplicationBuilder(args);
            _ = builder.Logging.SetMinimumLevel(LogLevel.Information);

            // Shared data layer + server-tier provider/orchestrator registrations (session provider,
            // active provider's four services, download/evidence orchestrators, JammingToolsOrchestrator) -
            // see VideoForensics.Hosting/VideoForensicsHostingExtensions.cs. MCP remains a
            // server-tier host (it talks to the active provider directly), unaffected by the client/server split
            // that only applies to the planned MAUI app.
            // The active provider is read from IConfiguration's "ActiveProvider" setting (from appsettings.json
            // or the VIDEOFORENSICS_ActiveProvider environment variable), defaulting to "Ring" for backward compatibility.
            _ = builder.Services.AddVideoForensicsDataLayer();
            _ = builder.Services.AddVideoForensicsServerCore(builder.Configuration["ActiveProvider"] ?? "Ring");

            // Forensics query repositories (Phases 1-4) - MCP-specific, not shared with other hosts
            _ = builder.Services.AddScoped<ITimelineRepository, TimelineRepository>();
            _ = builder.Services.AddScoped<IIntegrityRepository, IntegrityRepository>();
            _ = builder.Services.AddScoped<ICorrelationRepository, CorrelationRepository>();
            _ = builder.Services.AddScoped<IAuditTrailRepository, AuditTrailRepository>();

            // MCP Tool classes (Phases 1-4)
            _ = builder.Services.AddScoped<VideoForensics.Mcp.Tools.TimelineTools>();
            _ = builder.Services.AddScoped<VideoForensics.Mcp.Tools.IntegrityTools>();
            _ = builder.Services.AddScoped<VideoForensics.Mcp.Tools.CorrelationTools>();
            _ = builder.Services.AddScoped<VideoForensics.Mcp.Tools.AuditTrailTools>();
            _ = builder.Services.AddScoped<VideoForensics.Mcp.Tools.JammingTools>();

            // MCP server: stdio transport, attribute-discovered tools/resources
            _ = builder.Services
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

            // LAZY: DB init + Events backfill + persisted-config load, in that order, without
            // blocking MCP startup - see VideoForensicsHostingExtensions.InitializeVideoForensicsDataAsync.
            var initTask = Task.Run(async () =>
            {
                try
                {
                    await VideoForensicsHostingExtensions.InitializeVideoForensicsDataAsync(host.Services, initLogger, CancellationToken.None);
                    initLogger.LogInformation("Deferred initialization (DB, Events backfill, config) completed.");
                }
                catch (Exception ex)
                {
                    initLogger.LogCritical(ex, "Deferred initialization failed.");
                }
            });

            initLogger.LogInformation("Database initialization started in background. MCP server can respond to requests immediately.");

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

            initLogger.LogInformation("All tool classes (Timeline, Integrity, Correlation, Audit, Jamming) configured with [McpServerToolType]");
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
