namespace VideoForensics.Client.Core.Tools
{
    using Microsoft.Extensions.Logging;
    using VideoForensics.Client.Common;
    using VideoForensics.Data.Common.Contracts;

    public class ConfigToolsOrchestrator
    {
        private readonly ILogger<ConfigToolsOrchestrator> _logger;
        private readonly IForensicsConfigurationService _configService;
        private readonly IAppSettingRepository? _settingRepository;

        public ConfigToolsOrchestrator(
            ILogger<ConfigToolsOrchestrator> logger,
            IForensicsConfigurationService configService,
            IAppSettingRepository? settingRepository = null)
        {
            _logger = logger;
            _configService = configService;
            _settingRepository = settingRepository;
        }

        public async Task<(bool Success, string Message)> SetRetentionDaysAsync(
            IForensicsConfiguration config,
            int days,
            CancellationToken ct = default)
        {
            if (days <= 0)
                return (false, "Retention days must be greater than 0");

            config.RetentionDaysDefault = days;
            await _configService.SaveConfigurationAsync(config, ct);
            _logger.LogInformation("Retention period set to {days} days", days);
            return (true, $"Retention period updated to {days} days");
        }

        public async Task<(bool Success, string Message)> SetMaxConcurrentDownloadsAsync(
            IForensicsConfiguration config,
            int count,
            CancellationToken ct = default)
        {
            if (count < 1)
                return (false, "Max concurrent downloads must be at least 1");

            config.MaxConcurrentDownloads = count;
            await _configService.SaveConfigurationAsync(config, ct);
            _logger.LogInformation("Max concurrent downloads set to {count}", count);
            return (true, $"Max concurrent downloads updated to {count}");
        }

        public async Task<(bool Success, string Message)> SetDownloadLocationAsync(
            IForensicsConfiguration config,
            string path,
            CancellationToken ct = default)
        {
            try
            {
                if (!Directory.Exists(path))
                    Directory.CreateDirectory(path);

                config.DownloadLocation = path;
                await _configService.SaveConfigurationAsync(config, ct);
                _logger.LogInformation("Download location set to {path}", path);
                return (true, $"Download location updated to {path}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set download location to {path}", path);
                return (false, $"Failed to set download location: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message)> SetReportEnabledAsync(
            IForensicsConfiguration config,
            string reportType,
            bool enabled,
            CancellationToken ct = default)
        {
            var property = reportType switch
            {
                "ForensicAnalysis" => nameof(IForensicsConfiguration.EnableForensicAnalysisReports),
                "SignalAnomaly" => nameof(IForensicsConfiguration.EnableSignalAnomalyReports),
                "ChainOfCustody" => nameof(IForensicsConfiguration.EnableChainOfCustodyReports),
                "EvidenceValidation" => nameof(IForensicsConfiguration.EnableEvidenceValidationReports),
                "AccessControl" => nameof(IForensicsConfiguration.EnableAccessControlMonitoring),
                _ => null
            };

            if (property == null)
                return (false, $"Unknown report type: {reportType}");

            var configObj = (ForensicsConfiguration)config;
            var propInfo = typeof(ForensicsConfiguration).GetProperty(property);
            if (propInfo != null)
            {
                propInfo.SetValue(configObj, enabled);
                await _configService.SaveConfigurationAsync(configObj, ct);
                _logger.LogInformation("{reportType} reports {status}", reportType, enabled ? "enabled" : "disabled");
                return (true, $"{reportType} reports {(enabled ? "enabled" : "disabled")}");
            }

            return (false, $"Could not update {reportType}");
        }

        public async Task<(bool Success, string Message)> SetRedactionLevelAsync(
            IForensicsConfiguration config,
            RedactionLevel level,
            CancellationToken ct = default)
        {
            config.RedactionLevel = level;
            await _configService.SaveConfigurationAsync(config, ct);
            _logger.LogInformation("Redaction level set to {level}", level);
            return (true, $"Redaction level updated to {level}");
        }

        public async Task<(bool Success, string Message)> SetKeyStorageProviderAsync(
            IForensicsConfiguration config,
            KeyStorageProvider provider,
            CancellationToken ct = default)
        {
            config.KeyStorageProvider = provider;
            await _configService.SaveConfigurationAsync(config, ct);
            _logger.LogInformation("Key storage provider set to {provider}", provider);
            return (true, $"Key storage provider updated to {provider}");
        }

        public async Task<(bool Success, string Message)> SetLoggingLevelAsync(
            IForensicsConfiguration config,
            string level,
            CancellationToken ct = default)
        {
            config.LogLevel = level;
            await _configService.SaveConfigurationAsync(config, ct);
            _logger.LogInformation("Logging level set to {level}", level);
            return (true, $"Logging level updated to {level}");
        }

        public async Task<(bool Success, string Message)> SetReportFormatAsync(
            IForensicsConfiguration config,
            string format,
            CancellationToken ct = default)
        {
            if (!new[] { "json", "xml", "csv" }.Contains(format, StringComparer.OrdinalIgnoreCase))
                return (false, "Report format must be json, xml, or csv");

            config.ReportOutputFormat = format;
            await _configService.SaveConfigurationAsync(config, ct);
            _logger.LogInformation("Report format set to {format}", format);
            return (true, $"Report format updated to {format}");
        }

        public async Task<(bool Success, string Message)> FactoryResetAsync(CancellationToken ct = default)
        {
            try
            {
                var downloadDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Pictures",
                    "VideoForensics");

                if (Directory.Exists(downloadDir))
                {
                    _logger.LogInformation("Deleting download directory: {DownloadDir}", downloadDir);
                    Directory.Delete(downloadDir, recursive: true);
                }

                var dbPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "VideoForensics",
                    "videoforensics.db");

                if (File.Exists(dbPath))
                {
                    _logger.LogInformation("Deleting database: {DbPath}", dbPath);
                    File.Delete(dbPath);
                }

                _logger.LogInformation("Factory reset completed successfully");
                return (true, "Factory reset completed. All data has been removed.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Factory reset failed");
                return (false, $"Factory reset failed: {ex.Message}");
            }
        }
    }
}
