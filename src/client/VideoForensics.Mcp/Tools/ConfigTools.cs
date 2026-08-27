using System.ComponentModel;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using VideoForensics.Client.Common;

namespace VideoForensics.Mcp.Tools
{
    /// <summary>MCP tools for reading and updating persisted VideoForensics configuration.</summary>
    [McpServerToolType]
    public static class ConfigTools
    {
        [McpServerTool, Description("Returns the current VideoForensics configuration: report toggles, PII redaction settings, key storage provider, retention policy, download location, concurrency, and log level.")]
        public static object GetConfiguration(IForensicsConfiguration config)
        {
            return new
            {
                config.EnableForensicAnalysisReports,
                config.EnableSignalAnomalyReports,
                config.EnableChainOfCustodyReports,
                config.EnableEvidenceValidationReports,
                config.EnableAccessControlMonitoring,
                config.EnablePiiRedaction,
                config.ReportsDirectory,
                config.ReportOutputFormat,
                config.DownloadLocation,
                RedactionLevel = config.RedactionLevel.ToString(),
                KeyStorageProvider = config.KeyStorageProvider.ToString(),
                config.RetentionDaysDefault,
                config.LogLevel,
                config.MaxConcurrentDownloads,
                config.RescanWindowDays
            };
        }

        [McpServerTool, Description("Enables or disables generation of a specific report type (Forensic, SignalAnomaly, ChainOfCustody, EvidenceValidation, AccessControl).")]
        public static async Task<string> SetReportEnabled(
            IForensicsConfiguration config,
            IForensicsConfigurationService configService,
            [Description("One of: Forensic, SignalAnomaly, ChainOfCustody, EvidenceValidation, AccessControl")] string reportType,
            bool enabled,
            CancellationToken cancellationToken)
        {
            switch (reportType.Trim().ToLowerInvariant())
            {
                case "forensic":
                    config.EnableForensicAnalysisReports = enabled; break;
                case "signalanomaly":
                    config.EnableSignalAnomalyReports = enabled; break;
                case "chainofcustody":
                    config.EnableChainOfCustodyReports = enabled; break;
                case "evidencevalidation":
                    config.EnableEvidenceValidationReports = enabled; break;
                case "accesscontrol":
                    config.EnableAccessControlMonitoring = enabled; break;
                default:
                    return $"Unknown report type '{reportType}'. Expected one of: Forensic, SignalAnomaly, ChainOfCustody, EvidenceValidation, AccessControl.";
            }

            await configService.SaveConfigurationAsync(config, cancellationToken);
            return $"{reportType} reports {(enabled ? "enabled" : "disabled")}.";
        }

        [McpServerTool, Description("Sets the output directory and/or format ('json', 'xml', or 'csv') for generated reports.")]
        public static async Task<string> SetReportOutputSettings(
            IForensicsConfiguration config,
            IForensicsConfigurationService configService,
            CancellationToken cancellationToken,
            [Description("Directory reports are written to; omit to leave unchanged")] string? reportsDirectory = null,
            [Description("'json', 'xml', or 'csv'; omit to leave unchanged")] string? reportOutputFormat = null)
        {
            if (reportsDirectory != null) config.ReportsDirectory = reportsDirectory;
            if (reportOutputFormat != null)
            {
                if (reportOutputFormat is not ("json" or "xml" or "csv"))
                {
                    return "reportOutputFormat must be one of: json, xml, csv.";
                }
                config.ReportOutputFormat = reportOutputFormat;
            }

            await configService.SaveConfigurationAsync(config, cancellationToken);
            return "Report output settings updated.";
        }

        [McpServerTool, Description("Enables/disables PII redaction and/or sets the redaction level applied to exported reports.")]
        public static async Task<string> SetPiiRedaction(
            IForensicsConfiguration config,
            IForensicsConfigurationService configService,
            CancellationToken cancellationToken,
            [Description("Enable or disable PII redaction; omit to leave unchanged")] bool? enabled = null,
            [Description("One of: None, Light, Medium, Heavy; omit to leave unchanged")] RedactionLevel? level = null)
        {
            if (enabled.HasValue) config.EnablePiiRedaction = enabled.Value;
            if (level.HasValue) config.RedactionLevel = level.Value;

            await configService.SaveConfigurationAsync(config, cancellationToken);
            return $"PII redaction: enabled={config.EnablePiiRedaction}, level={config.RedactionLevel}.";
        }

        [McpServerTool, Description("Sets the key storage provider used for encrypting credentials/keys (Auto, Tpm, Dpapi, or FileBased).")]
        public static async Task<string> SetKeyStorageProvider(
            IForensicsConfiguration config,
            IForensicsConfigurationService configService,
            KeyStorageProvider provider,
            CancellationToken cancellationToken)
        {
            config.KeyStorageProvider = provider;
            await configService.SaveConfigurationAsync(config, cancellationToken);
            return $"Key storage provider set to {provider}.";
        }

        [McpServerTool, Description("Sets the default evidence retention period, in days.")]
        public static async Task<string> SetRetentionDays(
            IForensicsConfiguration config,
            IForensicsConfigurationService configService,
            [Description("Retention period in days; must be greater than 0")] int days,
            CancellationToken cancellationToken)
        {
            if (days <= 0)
            {
                return "days must be greater than 0.";
            }

            config.RetentionDaysDefault = days;
            await configService.SaveConfigurationAsync(config, cancellationToken);
            return $"Retention period set to {days} day(s).";
        }

        [McpServerTool, Description("Sets the directory downloaded evidence files are saved to.")]
        public static async Task<string> SetDownloadLocation(
            IForensicsConfiguration config,
            IForensicsConfigurationService configService,
            [Description("Directory path for downloaded evidence files")] string downloadLocation,
            CancellationToken cancellationToken)
        {
            try
            {
                Directory.CreateDirectory(downloadLocation);
            }
            catch (Exception ex)
            {
                return $"Failed to set download location: {ex.Message}";
            }

            config.DownloadLocation = downloadLocation;
            await configService.SaveConfigurationAsync(config, cancellationToken);
            return $"Download location set to {downloadLocation}.";
        }

        [McpServerTool, Description("Sets the maximum number of concurrent downloads per device.")]
        public static async Task<string> SetMaxConcurrentDownloads(
            IForensicsConfiguration config,
            IForensicsConfigurationService configService,
            [Description("Must be at least 1")] int maxConcurrentDownloads,
            CancellationToken cancellationToken)
        {
            if (maxConcurrentDownloads < 1)
            {
                return "maxConcurrentDownloads must be at least 1.";
            }

            config.MaxConcurrentDownloads = maxConcurrentDownloads;
            await configService.SaveConfigurationAsync(config, cancellationToken);
            return $"Max concurrent downloads set to {maxConcurrentDownloads}.";
        }

        [McpServerTool, Description("Sets the application log level (Debug, Information, Warning, Error, or Critical).")]
        public static async Task<string> SetLogLevel(
            IForensicsConfiguration config,
            IForensicsConfigurationService configService,
            [Description("One of: Debug, Information, Warning, Error, Critical")] string logLevel,
            CancellationToken cancellationToken)
        {
            var allowed = new[] { "Debug", "Information", "Warning", "Error", "Critical" };
            if (!allowed.Contains(logLevel, StringComparer.OrdinalIgnoreCase))
            {
                return $"logLevel must be one of: {string.Join(", ", allowed)}.";
            }

            config.LogLevel = logLevel;
            await configService.SaveConfigurationAsync(config, cancellationToken);
            return $"Log level set to {logLevel}. (Takes effect on next server restart.)";
        }

        [McpServerTool, Description("DESTRUCTIVE / IRREVERSIBLE: Deletes all downloaded evidence files, the entire evidence database (all data and settings), and legacy config files, then stops the MCP server process. This cannot be undone. Requires confirm=true or nothing is performed.")]
        public static string FactoryReset(
            IForensicsConfiguration config,
            ILogger<object> logger,
            [Description("Must be true to perform the reset. If false or omitted, this call does nothing.")] bool confirm = false)
        {
            if (!confirm)
            {
                return "Factory reset not performed: confirm=true is required. Ask the user to explicitly confirm this destructive, irreversible action before calling FactoryReset again with confirm=true.";
            }

            try
            {
                var downloadDir = config.DownloadLocation ?? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Pictures",
                    "VideoForensics");

                if (Directory.Exists(downloadDir))
                {
                    Directory.Delete(downloadDir, recursive: true);
                }

                var dbPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "VideoForensics",
                    "videoforensics.db");

                if (File.Exists(dbPath))
                {
                    // Release the pooled SQLite connection's OS-level lock on the file (and its
                    // WAL/SHM sidecars) before deleting — see MenuManager.PerformFactoryReset for the
                    // same fix and why it's needed.
                    SqliteConnection.ClearAllPools();
                    File.Delete(dbPath);

                    foreach (var sidecar in new[] { dbPath + "-wal", dbPath + "-shm", dbPath + "-journal" })
                    {
                        if (File.Exists(sidecar))
                        {
                            File.Delete(sidecar);
                        }
                    }
                }

                var legacyConfigPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "VideoForensics",
                    "ForensicsConfig.json");

                if (File.Exists(legacyConfigPath))
                {
                    File.Delete(legacyConfigPath);
                }

                logger.LogInformation("Factory reset completed via MCP. Exiting process.");

                // Mirrors the console client's behavior of exiting after a factory reset. The MCP
                // client will observe the server process end; it must be relaunched to continue.
                _ = Task.Run(async () =>
                {
                    await Task.Delay(250);
                    Environment.Exit(0);
                });

                return "Factory reset complete: downloaded evidence, the database, and legacy settings were deleted. The MCP server process is stopping and must be relaunched to continue.";
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Factory reset failed");
                return $"Factory reset failed: {ex.Message}";
            }
        }
    }
}
