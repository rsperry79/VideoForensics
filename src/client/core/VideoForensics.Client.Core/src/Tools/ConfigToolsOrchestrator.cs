using Microsoft.Extensions.Logging;

using System.Reflection;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Core.Helpers.Platform;
using VideoForensics.Data.Common.Contracts;

namespace VideoForensics.Client.Core.Tools
{
    public class ConfigToolsOrchestrator : VideoForensics.Client.Core.Contracts.IConfigToolsOrchestrator
    {
        private readonly ILogger<ConfigToolsOrchestrator> _logger;
        private readonly IForensicsConfigurationService _configService;
        private readonly IAppSettingRepository? _settingRepository;
        private readonly IDatabaseMaintenanceService? _maintenanceService;

        public ConfigToolsOrchestrator(
            ILogger<ConfigToolsOrchestrator> logger,
            IForensicsConfigurationService configService,
            IAppSettingRepository? settingRepository = null,
            IDatabaseMaintenanceService? maintenanceService = null)
        {
            _logger = logger;
            _configService = configService;
            _settingRepository = settingRepository;
            _maintenanceService = maintenanceService;
        }

        public async Task<(bool Success, string Message)> SetRetentionDaysAsync(
            IForensicsConfiguration config,
            int days,
            CancellationToken ct = default)
        {
            if (days <= 0)
            {
                return (false, "Retention days must be greater than 0");
            }

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
            {
                return (false, "Max concurrent downloads must be at least 1");
            }

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
                {
                    _ = Directory.CreateDirectory(path);
                }

                config.DownloadLocation = path;
                await _configService.SaveConfigurationAsync(config, ct);
                _logger.LogInformation("Download location set to {path}", path);
                return (true, $"Download location updated to {path}");
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to set download location to {path}: {Message} ({ErrorType})", path, ex.Message, ex.GetType().Name);
                return (false, $"Failed to set download location: {ex.Message}");
            }
        }

        public async Task<(bool Success, string Message)> SetReportEnabledAsync(
            IForensicsConfiguration config,
            string reportType,
            bool enabled,
            CancellationToken ct = default)
        {
            string? property = reportType switch
            {
                "ForensicAnalysis" => nameof(IForensicsConfiguration.EnableForensicAnalysisReports),
                "SignalAnomaly" => nameof(IForensicsConfiguration.EnableSignalAnomalyReports),
                "ChainOfCustody" => nameof(IForensicsConfiguration.EnableChainOfCustodyReports),
                "EvidenceValidation" => nameof(IForensicsConfiguration.EnableEvidenceValidationReports),
                "AccessControl" => nameof(IForensicsConfiguration.EnableAccessControlMonitoring),
                _ => null
            };

            if (property == null)
            {
                return (false, $"Unknown report type: {reportType}");
            }

            var configObj = (ForensicsConfiguration)config;
            PropertyInfo? propInfo = typeof(ForensicsConfiguration).GetProperty(property);
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

        public async Task<(bool Success, string Message)> SetHealthSyncAsync(
            IForensicsConfiguration config,
            bool enabled,
            CancellationToken ct = default)
        {
            config.EnableHealthSync = enabled;
            await _configService.SaveConfigurationAsync(config, ct);
            _logger.LogInformation("Health sync set to {enabled}", enabled);
            return (true, enabled ? "Health sync enabled" : "Health sync disabled");
        }

        public async Task<(bool Success, string Message)> SetScheduledReportsEnabledAsync(
            IForensicsConfiguration config,
            bool enabled,
            CancellationToken ct = default)
        {
            config.EnableScheduledReports = enabled;
            await _configService.SaveConfigurationAsync(config, ct);
            _logger.LogInformation("Scheduled reports set to {enabled}", enabled);
            return (true, enabled ? "Scheduled reports enabled" : "Scheduled reports disabled");
        }

        public async Task<(bool Success, string Message)> SetScheduledReportIntervalHoursAsync(
            IForensicsConfiguration config,
            int hours,
            CancellationToken ct = default)
        {
            if (hours is < 1 or > 168)
            {
                return (false, "Scheduled report interval must be between 1 and 168 hours");
            }

            config.ScheduledReportIntervalHours = hours;
            await _configService.SaveConfigurationAsync(config, ct);
            _logger.LogInformation("Scheduled report interval set to {hours} hours", hours);
            return (true, $"Scheduled reports will run every {hours} hours");
        }

        public async Task<(bool Success, string Message)> SetServiceIntervalHoursAsync(
            IForensicsConfiguration config,
            string service,
            int hours,
            CancellationToken ct = default)
        {
            if (hours is < 1 or > 168)
            {
                return (false, "Service interval must be between 1 and 168 hours");
            }

            switch (service)
            {
                case "LogPrune":
                    config.LogPruneIntervalHours = hours;
                    break;
                case "UpdateCheck":
                    config.UpdateCheckIntervalHours = hours;
                    break;
                case "MediaBackfill":
                    config.MediaBackfillIntervalHours = hours;
                    break;
                default:
                    return (false, $"Unknown service: {service}");
            }

            await _configService.SaveConfigurationAsync(config, ct);
            _logger.LogInformation("Service {service} interval set to {hours} hours", service, hours);
            return (true, $"{service} will run every {hours} hours");
        }

        public async Task<(bool Success, string Message)> SetServiceIntervalMinutesAsync(
            IForensicsConfiguration config,
            string service,
            int minutes,
            CancellationToken ct = default)
        {
            if (minutes is < 1 or > 1440)
            {
                return (false, "Service interval must be between 1 and 1440 minutes");
            }

            switch (service)
            {
                case "Jamming":
                    config.JammingIntervalMinutes = minutes;
                    break;
                case "Codec":
                    config.CodecIntervalMinutes = minutes;
                    break;
                case "Flag":
                    config.FlagIntervalMinutes = minutes;
                    break;
                case "DeviceHealth":
                    config.DeviceHealthIntervalMinutes = minutes;
                    break;
                case "OfflineRepoll":
                    config.OfflineRepollIntervalMinutes = minutes;
                    break;
                default:
                    return (false, $"Unknown service: {service}");
            }

            await _configService.SaveConfigurationAsync(config, ct);
            _logger.LogInformation("Service {service} interval set to {minutes} minutes", service, minutes);
            return (true, $"{service} will run every {minutes} minutes");
        }

        public async Task<(bool Success, string Message)> SetScheduledSyncTickSecondsAsync(
            IForensicsConfiguration config,
            int seconds,
            CancellationToken ct = default)
        {
            if (seconds is < 5 or > 3600)
            {
                return (false, "Scheduled sync tick must be between 5 and 3600 seconds");
            }

            config.ScheduledSyncTickSeconds = seconds;
            await _configService.SaveConfigurationAsync(config, ct);
            _logger.LogInformation("Scheduled sync tick set to {seconds} seconds", seconds);
            return (true, $"Scheduled sync tick will run every {seconds} seconds");
        }

        public async Task<(bool Success, string Message)> SetTimelapseCheckIntervalMinutesAsync(
            IForensicsConfiguration config,
            int minutes,
            CancellationToken ct = default)
        {
            if (minutes is < 1 or > 1440)
            {
                return (false, "Time-lapse interval must be between 1 and 1440 minutes");
            }

            config.TimelapseCheckIntervalMinutes = minutes;
            await _configService.SaveConfigurationAsync(config, ct);
            _logger.LogInformation("Time-lapse check interval set to {minutes} minutes", minutes);
            return (true, $"Time-lapses will be built every {minutes} minutes");
        }

        public async Task<(bool Success, string Message)> SetTimelapseMaxSegmentMinutesAsync(
            IForensicsConfiguration config,
            int minutes,
            CancellationToken ct = default)
        {
            if (minutes is < 1 or > 1440)
            {
                return (false, "Time-lapse maximum segment must be between 1 and 1440 minutes");
            }

            config.TimelapseMaxSegmentMinutes = minutes;
            await _configService.SaveConfigurationAsync(config, ct);
            _logger.LogInformation("Time-lapse max segment set to {minutes} minutes", minutes);
            return (true, $"Time-lapse segments will be at most {minutes} minutes");
        }

        public async Task<(bool Success, string Message)> SetMdnsAdvertisementAsync(
            IForensicsConfiguration config,
            bool enabled,
            CancellationToken ct = default)
        {
            config.EnableMdnsAdvertisement = enabled;
            await _configService.SaveConfigurationAsync(config, ct);
            _logger.LogInformation("mDNS advertisement set to {enabled}", enabled);
            return (true, enabled ? "mDNS advertisement enabled" : "mDNS advertisement disabled");
        }

        public async Task<(bool Success, string Message)> SetReportFormatAsync(
            IForensicsConfiguration config,
            string format,
            CancellationToken ct = default)
        {
            if (!new[] { "json", "xml", "csv" }.Contains(format, StringComparer.OrdinalIgnoreCase))
            {
                return (false, "Report format must be json, xml, or csv");
            }

            config.ReportOutputFormat = format;
            await _configService.SaveConfigurationAsync(config, ct);
            _logger.LogInformation("Report format set to {format}", format);
            return (true, $"Report format updated to {format}");
        }

        /// <param name="downloadDirOverride">Overrides the download directory to delete. Defaults to
        /// ~/Pictures/VideoForensics. Exists so tests can exercise this against a throwaway
        /// directory instead of the real one.</param>
        /// <param name="dbPathOverride">Overrides the database file to delete. Defaults to
        /// the database root from StorageLocationProvider (videoforensics.db). Exists so tests can exercise this against a
        /// throwaway file instead of the real, live application database.</param>
        public async Task<(bool Success, string Message)> FactoryResetAsync(
            CancellationToken ct = default,
            string? downloadDirOverride = null,
            string? dbPathOverride = null)
        {
            try
            {
                string downloadDir = downloadDirOverride ?? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Pictures",
                    "VideoForensics");

                if (Directory.Exists(downloadDir))
                {
                    _logger.LogDebug("Deleting download directory: {DownloadDir}", downloadDir);
                    Directory.Delete(downloadDir, recursive: true);
                }

                // Same folder AddVideoForensicsSqlite opens (StorageCategory.Database), so a reset deletes the live file.
                string dbPath = dbPathOverride ?? Path.Combine(
                    new StorageLocationProvider().GetDefaultRoot(StorageCategory.Database),
                    "videoforensics.db");

                if (File.Exists(dbPath))
                {
                    // The app holds the DB file open via a pooled SQLite connection for its entire
                    // lifetime (see AddVideoForensicsSqlite's Pooling=true), so File.Delete fails with
                    // "being used by another process" unless every pooled connection is released
                    // first - disposing a DbContext alone only returns its connection to the pool,
                    // it doesn't release the OS-level file handle.
                    if (_maintenanceService != null)
                    {
                        _logger.LogDebug("Releasing database connections before deletion");
                        await _maintenanceService.ReleaseAllConnectionsAsync(ct);
                    }

                    _logger.LogDebug("Deleting database: {DbPath}", dbPath);
                    File.Delete(dbPath);

                    // WAL mode leaves -wal/-shm sidecar files alongside the main database file;
                    // deleting only the main file leaves stale ones behind.
                    foreach (string? sidecar in new[] { $"{dbPath}-wal", $"{dbPath}-shm" })
                    {
                        if (File.Exists(sidecar))
                        {
                            File.Delete(sidecar);
                        }
                    }
                }

                _logger.LogInformation("Factory reset completed successfully");
                return (true, "Factory reset completed. All data has been removed.");
            }
            catch (Exception ex)
            {
                _logger.LogError("Factory reset failed: {Message} ({ErrorType})", ex.Message, ex.GetType().Name);
                return (false, $"Factory reset failed: {ex.Message}");
            }
        }
    }
}
