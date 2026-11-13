using VideoForensics.Client.Common.Contracts;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>Extension methods for mapping configuration objects to/from ClientConfigDtos.</summary>
    public static class ClientConfigDtoMapping
    {
        /// <summary>
        /// Converts an IForensicsConfiguration to a ClientConfigDto.
        /// </summary>
        public static ClientConfigDto ToDto(this IForensicsConfiguration config)
        {
            return new ClientConfigDto(
                EnableForensicAnalysisReports: config.EnableForensicAnalysisReports,
                EnableSignalAnomalyReports: config.EnableSignalAnomalyReports,
                EnableChainOfCustodyReports: config.EnableChainOfCustodyReports,
                EnableEvidenceValidationReports: config.EnableEvidenceValidationReports,
                EnableAccessControlMonitoring: config.EnableAccessControlMonitoring,
                EnablePiiRedaction: config.EnablePiiRedaction,
                ReportOutputFormat: config.ReportOutputFormat,
                RedactionLevel: Enum.Parse<RedactionLevelDto>(config.RedactionLevel.ToString()),
                RetentionDaysDefault: config.RetentionDaysDefault,
                LogLevel: config.LogLevel,
                MaxConcurrentDownloads: config.MaxConcurrentDownloads,
                DownloadStartDate: config.DownloadStartDate,
                EnableHealthSync: config.EnableHealthSync,
                EnableScheduledReports: config.EnableScheduledReports,
                ScheduledReportIntervalHours: config.ScheduledReportIntervalHours,
                LogPruneIntervalHours: config.LogPruneIntervalHours,
                MediaBackfillIntervalHours: config.MediaBackfillIntervalHours,
                TimelapseCheckIntervalMinutes: config.TimelapseCheckIntervalMinutes,
                TimelapseMaxSegmentMinutes: config.TimelapseMaxSegmentMinutes,
                JammingIntervalMinutes: config.JammingIntervalMinutes,
                CodecIntervalMinutes: config.CodecIntervalMinutes,
                FlagIntervalMinutes: config.FlagIntervalMinutes,
                DeviceHealthIntervalMinutes: config.DeviceHealthIntervalMinutes,
                OfflineRepollIntervalMinutes: config.OfflineRepollIntervalMinutes,
                ScheduledSyncTickSeconds: config.ScheduledSyncTickSeconds,
                EnableMdnsAdvertisement: config.EnableMdnsAdvertisement,
                EnableEmailNotifications: config.EnableEmailNotifications,
                ConfiguredNetworkTier: Enum.Parse<NetworkTierDto>(config.ConfiguredNetworkTier.ToString()),
                InternetServerUrl: config.InternetServerUrl,
                EnableLiveView: config.EnableLiveView,
                LiveViewIdleTimeoutMinutes: config.LiveViewIdleTimeoutMinutes,
                LiveViewTelemetrySampleIntervalSeconds: config.LiveViewTelemetrySampleIntervalSeconds,
                SustainedModeMaxDurationMinutes: config.SustainedModeMaxDurationMinutes,
                LiveViewTelemetryRetentionDays: config.LiveViewTelemetryRetentionDays,
                ElevatedPollingWindowMinutes: config.ElevatedPollingWindowMinutes,
                ElevatedPollingIntervalSeconds: config.ElevatedPollingIntervalSeconds,
                EnableBitrateCalibration: config.EnableBitrateCalibration,
                CalibrationCheckIntervalMinutes: config.CalibrationCheckIntervalMinutes,
                CalibrationSessionDurationSeconds: config.CalibrationSessionDurationSeconds,
                MinCalibrationSamplesPerBucket: config.MinCalibrationSamplesPerBucket,
                CalibrationStalenessDays: config.CalibrationStalenessDays
            );
        }

        /// <summary>
        /// Converts a ClientConfigDto back to a configuration object. Only the client-safe allowlist is set; every other
        /// setting keeps its default, because the server never sends it. A client must not persist the result.
        /// </summary>
        public static ForensicsConfiguration ToDomain(this ClientConfigDto dto)
        {
            return new ForensicsConfiguration
            {
                EnableForensicAnalysisReports = dto.EnableForensicAnalysisReports,
                EnableSignalAnomalyReports = dto.EnableSignalAnomalyReports,
                EnableChainOfCustodyReports = dto.EnableChainOfCustodyReports,
                EnableEvidenceValidationReports = dto.EnableEvidenceValidationReports,
                EnableAccessControlMonitoring = dto.EnableAccessControlMonitoring,
                EnablePiiRedaction = dto.EnablePiiRedaction,
                ReportOutputFormat = dto.ReportOutputFormat,
                RedactionLevel = Enum.Parse<VideoForensics.Client.Common.Contracts.RedactionLevel>(dto.RedactionLevel.ToString()),
                RetentionDaysDefault = dto.RetentionDaysDefault,
                LogLevel = dto.LogLevel,
                MaxConcurrentDownloads = dto.MaxConcurrentDownloads,
                DownloadStartDate = dto.DownloadStartDate,
                EnableHealthSync = dto.EnableHealthSync,
                EnableScheduledReports = dto.EnableScheduledReports,
                ScheduledReportIntervalHours = dto.ScheduledReportIntervalHours,
                LogPruneIntervalHours = dto.LogPruneIntervalHours,
                MediaBackfillIntervalHours = dto.MediaBackfillIntervalHours,
                TimelapseCheckIntervalMinutes = dto.TimelapseCheckIntervalMinutes,
                TimelapseMaxSegmentMinutes = dto.TimelapseMaxSegmentMinutes,
                JammingIntervalMinutes = dto.JammingIntervalMinutes,
                CodecIntervalMinutes = dto.CodecIntervalMinutes,
                FlagIntervalMinutes = dto.FlagIntervalMinutes,
                DeviceHealthIntervalMinutes = dto.DeviceHealthIntervalMinutes,
                OfflineRepollIntervalMinutes = dto.OfflineRepollIntervalMinutes,
                ScheduledSyncTickSeconds = dto.ScheduledSyncTickSeconds,
                EnableMdnsAdvertisement = dto.EnableMdnsAdvertisement,
                EnableEmailNotifications = dto.EnableEmailNotifications,
                ConfiguredNetworkTier = Enum.Parse<VideoForensics.Data.Common.Entities.NetworkTier>(dto.ConfiguredNetworkTier.ToString()),
                InternetServerUrl = dto.InternetServerUrl,
                EnableLiveView = dto.EnableLiveView,
                LiveViewIdleTimeoutMinutes = dto.LiveViewIdleTimeoutMinutes,
                LiveViewTelemetrySampleIntervalSeconds = dto.LiveViewTelemetrySampleIntervalSeconds,
                SustainedModeMaxDurationMinutes = dto.SustainedModeMaxDurationMinutes,
                LiveViewTelemetryRetentionDays = dto.LiveViewTelemetryRetentionDays,
                ElevatedPollingWindowMinutes = dto.ElevatedPollingWindowMinutes,
                ElevatedPollingIntervalSeconds = dto.ElevatedPollingIntervalSeconds,
                EnableBitrateCalibration = dto.EnableBitrateCalibration,
                CalibrationCheckIntervalMinutes = dto.CalibrationCheckIntervalMinutes,
                CalibrationSessionDurationSeconds = dto.CalibrationSessionDurationSeconds,
                MinCalibrationSamplesPerBucket = dto.MinCalibrationSamplesPerBucket,
                CalibrationStalenessDays = dto.CalibrationStalenessDays
            };
        }
    }
}
