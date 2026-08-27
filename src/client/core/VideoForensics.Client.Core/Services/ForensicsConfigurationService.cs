namespace VideoForensics.Client.Core
{
    using Microsoft.Extensions.Logging;
    using VideoForensics.Client.Common;
    using VideoForensics.Data.Common.Contracts;

    public class ForensicsConfigurationService : IForensicsConfigurationService
    {
        private readonly ILogger<ForensicsConfigurationService> _logger;
        private readonly IAppSettingRepository _settingRepository;

        public ForensicsConfigurationService(ILogger<ForensicsConfigurationService> logger, IAppSettingRepository settingRepository)
        {
            _logger = logger;
            _settingRepository = settingRepository ?? throw new ArgumentNullException(nameof(settingRepository));
        }

        public async Task<IForensicsConfiguration> LoadConfigurationAsync(string configPath, CancellationToken cancellationToken = default)
        {
            var config = new ForensicsConfiguration();

            try
            {
                await LoadFromDatabaseAsync(config, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load configuration from database, using defaults");
            }

            return config;
        }

        public async Task SaveConfigurationAsync(IForensicsConfiguration config, CancellationToken cancellationToken = default)
        {
            await SaveToDatabaseAsync(config, cancellationToken);
        }

        private async Task LoadFromDatabaseAsync(ForensicsConfiguration config, CancellationToken cancellationToken)
        {
            try
            {
                config.EnableForensicAnalysisReports = await GetBoolSetting("EnableForensicAnalysisReports", config.EnableForensicAnalysisReports, cancellationToken);
                config.EnableSignalAnomalyReports = await GetBoolSetting("EnableSignalAnomalyReports", config.EnableSignalAnomalyReports, cancellationToken);
                config.EnableChainOfCustodyReports = await GetBoolSetting("EnableChainOfCustodyReports", config.EnableChainOfCustodyReports, cancellationToken);
                config.EnableEvidenceValidationReports = await GetBoolSetting("EnableEvidenceValidationReports", config.EnableEvidenceValidationReports, cancellationToken);
                config.EnableAccessControlMonitoring = await GetBoolSetting("EnableAccessControlMonitoring", config.EnableAccessControlMonitoring, cancellationToken);
                config.EnablePiiRedaction = await GetBoolSetting("EnablePiiRedaction", config.EnablePiiRedaction, cancellationToken);
                config.ReportsDirectory = await GetStringSetting("ReportsDirectory", config.ReportsDirectory, cancellationToken);
                config.ReportOutputFormat = await GetStringSetting("ReportOutputFormat", config.ReportOutputFormat, cancellationToken);
                config.DownloadLocation = await GetStringSetting("DownloadLocation", config.DownloadLocation, cancellationToken);
                config.RedactionLevel = await GetEnumSetting("RedactionLevel", config.RedactionLevel, cancellationToken);
                config.KeyStorageProvider = await GetEnumSetting("KeyStorageProvider", config.KeyStorageProvider, cancellationToken);
                config.RetentionDaysDefault = await GetIntSetting("RetentionDaysDefault", config.RetentionDaysDefault, cancellationToken);
                config.LogLevel = await GetStringSetting("LogLevel", config.LogLevel, cancellationToken);
                config.MaxConcurrentDownloads = await GetIntSetting("MaxConcurrentDownloads", config.MaxConcurrentDownloads, cancellationToken);
                config.RescanWindowDays = await GetIntSetting("RescanWindowDays", config.RescanWindowDays, cancellationToken);

                _logger.LogInformation("Configuration loaded from database");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load configuration from database");
                throw;
            }
        }

        private async Task SaveToDatabaseAsync(IForensicsConfiguration config, CancellationToken cancellationToken)
        {
            try
            {
                await _settingRepository!.SetAsync("EnableForensicAnalysisReports", config.EnableForensicAnalysisReports.ToString(), cancellationToken);
                await _settingRepository!.SetAsync("EnableSignalAnomalyReports", config.EnableSignalAnomalyReports.ToString(), cancellationToken);
                await _settingRepository!.SetAsync("EnableChainOfCustodyReports", config.EnableChainOfCustodyReports.ToString(), cancellationToken);
                await _settingRepository!.SetAsync("EnableEvidenceValidationReports", config.EnableEvidenceValidationReports.ToString(), cancellationToken);
                await _settingRepository!.SetAsync("EnableAccessControlMonitoring", config.EnableAccessControlMonitoring.ToString(), cancellationToken);
                await _settingRepository!.SetAsync("EnablePiiRedaction", config.EnablePiiRedaction.ToString(), cancellationToken);
                await _settingRepository!.SetAsync("ReportsDirectory", config.ReportsDirectory ?? "", cancellationToken);
                await _settingRepository!.SetAsync("ReportOutputFormat", config.ReportOutputFormat, cancellationToken);
                await _settingRepository!.SetAsync("DownloadLocation", config.DownloadLocation ?? "", cancellationToken);
                await _settingRepository!.SetAsync("RedactionLevel", config.RedactionLevel.ToString(), cancellationToken);
                await _settingRepository!.SetAsync("KeyStorageProvider", config.KeyStorageProvider.ToString(), cancellationToken);
                await _settingRepository!.SetAsync("RetentionDaysDefault", config.RetentionDaysDefault.ToString(), cancellationToken);
                await _settingRepository!.SetAsync("LogLevel", config.LogLevel, cancellationToken);
                await _settingRepository!.SetAsync("MaxConcurrentDownloads", config.MaxConcurrentDownloads.ToString(), cancellationToken);
                await _settingRepository!.SetAsync("RescanWindowDays", config.RescanWindowDays.ToString(), cancellationToken);

                _logger.LogInformation("Configuration saved to database");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save configuration to database");
                throw;
            }
        }


        private async Task<bool> GetBoolSetting(string key, bool defaultValue, CancellationToken ct)
        {
            var value = await _settingRepository!.GetAsync(key, ct);
            return string.IsNullOrEmpty(value) ? defaultValue : bool.Parse(value);
        }

        private async Task<string> GetStringSetting(string key, string? defaultValue, CancellationToken ct)
        {
            var value = await _settingRepository!.GetAsync(key, ct);
            return string.IsNullOrEmpty(value) ? (defaultValue ?? "") : value;
        }

        private async Task<int> GetIntSetting(string key, int defaultValue, CancellationToken ct)
        {
            var value = await _settingRepository!.GetAsync(key, ct);
            return string.IsNullOrEmpty(value) ? defaultValue : int.Parse(value);
        }

        private async Task<T> GetEnumSetting<T>(string key, T defaultValue, CancellationToken ct) where T : struct, Enum
        {
            var value = await _settingRepository!.GetAsync(key, ct);
            return string.IsNullOrEmpty(value) ? defaultValue : (T)Enum.Parse(typeof(T), value);
        }
    }
}
