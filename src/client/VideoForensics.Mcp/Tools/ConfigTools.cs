using System.ComponentModel;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using VideoForensics.Client.Common;
using VideoForensics.Client.Core.Tools;

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

        [McpServerTool, Description("Enables or disables generation of a specific report type (ForensicAnalysis, SignalAnomaly, ChainOfCustody, EvidenceValidation, AccessControl).")]
        public static async Task<string> SetReportEnabled(
            IForensicsConfiguration config,
            IForensicsConfigurationService configService,
            ConfigToolsOrchestrator orchestrator,
            [Description("One of: ForensicAnalysis, SignalAnomaly, ChainOfCustody, EvidenceValidation, AccessControl")] string reportType,
            bool enabled,
            CancellationToken cancellationToken)
        {
            var (success, message) = await orchestrator.SetReportEnabledAsync(config, reportType, enabled, cancellationToken);
            return message;
        }

        [McpServerTool, Description("Sets the output directory and/or format ('json', 'xml', or 'csv') for generated reports.")]
        public static async Task<string> SetReportOutputSettings(
            IForensicsConfiguration config,
            IForensicsConfigurationService configService,
            ConfigToolsOrchestrator orchestrator,
            CancellationToken cancellationToken,
            [Description("Directory reports are written to; omit to leave unchanged")] string? reportsDirectory = null,
            [Description("'json', 'xml', or 'csv'; omit to leave unchanged")] string? reportOutputFormat = null)
        {
            if (reportsDirectory != null)
            {
                var (_, msg) = await orchestrator.SetReportsDirectoryAsync(config, reportsDirectory, cancellationToken);
                if (!msg.StartsWith("Reports directory")) return msg;
            }

            if (reportOutputFormat != null)
            {
                var (_, msg) = await orchestrator.SetReportFormatAsync(config, reportOutputFormat, cancellationToken);
                if (!msg.StartsWith("Report format")) return msg;
            }

            return "Report output settings updated.";
        }

        [McpServerTool, Description("Enables/disables PII redaction and/or sets the redaction level applied to exported reports.")]
        public static async Task<string> SetPiiRedaction(
            IForensicsConfiguration config,
            IForensicsConfigurationService configService,
            ConfigToolsOrchestrator orchestrator,
            CancellationToken cancellationToken,
            [Description("Enable or disable PII redaction; omit to leave unchanged")] bool? enabled = null,
            [Description("One of: None, Light, Medium, Heavy; omit to leave unchanged")] RedactionLevel? level = null)
        {
            if (enabled.HasValue) config.EnablePiiRedaction = enabled.Value;
            if (level.HasValue)
            {
                var (success, msg) = await orchestrator.SetRedactionLevelAsync(config, level.Value, cancellationToken);
                if (!success) return msg;
            }

            await configService.SaveConfigurationAsync(config, cancellationToken);
            return $"PII redaction: enabled={config.EnablePiiRedaction}, level={config.RedactionLevel}.";
        }

        [McpServerTool, Description("Sets the key storage provider used for encrypting credentials/keys (Auto, Tpm, Dpapi, or FileBased).")]
        public static async Task<string> SetKeyStorageProvider(
            IForensicsConfiguration config,
            IForensicsConfigurationService configService,
            ConfigToolsOrchestrator orchestrator,
            KeyStorageProvider provider,
            CancellationToken cancellationToken)
        {
            var (success, message) = await orchestrator.SetKeyStorageProviderAsync(config, provider, cancellationToken);
            return message;
        }

        [McpServerTool, Description("Sets the default evidence retention period, in days.")]
        public static async Task<string> SetRetentionDays(
            IForensicsConfiguration config,
            IForensicsConfigurationService configService,
            ConfigToolsOrchestrator orchestrator,
            [Description("Retention period in days; must be greater than 0")] int days,
            CancellationToken cancellationToken)
        {
            var (success, message) = await orchestrator.SetRetentionDaysAsync(config, days, cancellationToken);
            return message;
        }

        [McpServerTool, Description("Sets the directory downloaded evidence files are saved to.")]
        public static async Task<string> SetDownloadLocation(
            IForensicsConfiguration config,
            IForensicsConfigurationService configService,
            ConfigToolsOrchestrator orchestrator,
            [Description("Directory path for downloaded evidence files")] string downloadLocation,
            CancellationToken cancellationToken)
        {
            var (success, message) = await orchestrator.SetDownloadLocationAsync(config, downloadLocation, cancellationToken);
            return message;
        }

        [McpServerTool, Description("Sets the maximum number of concurrent downloads per device.")]
        public static async Task<string> SetMaxConcurrentDownloads(
            IForensicsConfiguration config,
            IForensicsConfigurationService configService,
            ConfigToolsOrchestrator orchestrator,
            [Description("Must be at least 1")] int maxConcurrentDownloads,
            CancellationToken cancellationToken)
        {
            var (success, message) = await orchestrator.SetMaxConcurrentDownloadsAsync(config, maxConcurrentDownloads, cancellationToken);
            return message;
        }

        [McpServerTool, Description("Sets the application log level (Debug, Information, Warning, Error, or Critical).")]
        public static async Task<string> SetLogLevel(
            IForensicsConfiguration config,
            IForensicsConfigurationService configService,
            ConfigToolsOrchestrator orchestrator,
            [Description("One of: Debug, Information, Warning, Error, Critical")] string logLevel,
            CancellationToken cancellationToken)
        {
            var (success, message) = await orchestrator.SetLoggingLevelAsync(config, logLevel, cancellationToken);
            return message;
        }

        [McpServerTool, Description("DESTRUCTIVE / IRREVERSIBLE: Deletes all downloaded evidence files, the entire evidence database (all data and settings), and legacy config files, then stops the MCP server process. This cannot be undone. Requires confirm=true or nothing is performed.")]
        public static async Task<string> FactoryReset(
            IForensicsConfiguration config,
            ILogger<object> logger,
            ConfigToolsOrchestrator orchestrator,
            [Description("Must be true to perform the reset. If false or omitted, this call does nothing.")] bool confirm = false)
        {
            if (!confirm)
            {
                return "Factory reset not performed: confirm=true is required. Ask the user to explicitly confirm this destructive, irreversible action before calling FactoryReset again with confirm=true.";
            }

            var (success, message) = await orchestrator.FactoryResetAsync();
            if (!success) return message;

            try
            {
                logger.LogInformation("Factory reset completed via MCP. Exiting process.");
                _ = Task.Run(async () =>
                {
                    await Task.Delay(250);
                    Environment.Exit(0);
                });

                return "Factory reset complete: downloaded evidence, the database, and legacy settings were deleted. The MCP server process is stopping and must be relaunched to continue.";
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Factory reset exit failed");
                return $"Factory reset succeeded but process exit failed: {ex.Message}";
            }
        }
    }
}
