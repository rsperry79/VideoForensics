using Microsoft.Extensions.Logging;
using VideoForensics.Client.Common;

namespace VideoForensics.Client.Core
{
    /// <summary>Loads persisted configuration from the database onto a runtime instance.</summary>
    public static class ConfigurationLoader
    {
        /// <summary>
        /// Loads persisted configuration from the database and applies it to the runtime instance.
        /// This ensures all services that hold a reference to the runtime instance observe the loaded values.
        /// </summary>
        public static async Task LoadAndApplyAsync(
            IForensicsConfigurationService configService,
            ForensicsConfiguration runtimeConfig,
            ILogger logger,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var loadedConfig = await configService.LoadConfigurationAsync("", cancellationToken);

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
                runtimeConfig.MaxConcurrentDownloads = loadedConfig.MaxConcurrentDownloads;
                runtimeConfig.RescanWindowDays = loadedConfig.RescanWindowDays;

                logger.LogInformation("Configuration loaded from database successfully");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to load configuration from database; using defaults");
            }
        }
    }
}
