using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Api.Contracts
{
    /// <summary>
    /// Data transfer object for forensics configuration settings safe for a client device to read.
    /// This is an explicit allowlist of operational settings only—infrastructure details and credentials
    /// are excluded.
    /// </summary>
    /// <param name="EnableForensicAnalysisReports">Toggles generation of forensic analysis reports.</param>
    /// <param name="EnableSignalAnomalyReports">Toggles generation of signal anomaly detection reports.</param>
    /// <param name="EnableChainOfCustodyReports">Toggles generation of chain of custody reports.</param>
    /// <param name="EnableEvidenceValidationReports">Toggles generation of evidence validation reports.</param>
    /// <param name="EnableAccessControlMonitoring">Toggles access control event monitoring.</param>
    /// <param name="EnablePiiRedaction">Toggles PII redaction in reports and exports.</param>
    /// <param name="ReportOutputFormat">Format for report generation (e.g., "json", "pdf").</param>
    /// <param name="RedactionLevel">Level of PII redaction applied (None, Light, Medium, Heavy).</param>
    /// <param name="RetentionDaysDefault">Default number of days to retain evidence before automatic cleanup.</param>
    /// <param name="LogLevel">Minimum log level for server diagnostics (e.g., "Information", "Debug").</param>
    /// <param name="MaxConcurrentDownloads">Maximum number of simultaneous media downloads from providers.</param>
    /// <param name="DownloadStartDate">Start date for downloads as "yyyy-MM-dd" string; empty if unset.</param>
    /// <param name="ActiveProviderAccountId">ID of the currently active provider account, or null if none selected.</param>
    /// <param name="EnableHealthSync">Toggles periodic RSSI/device-health background sync.</param>
    /// <param name="EnableMdnsAdvertisement">Toggles mDNS advertisement on LAN (_videoforensics._tcp.local) for device discovery.</param>
    /// <param name="EnableEmailNotifications">Toggles the email notification channel for urgent security events.</param>
    /// <param name="ConfiguredNetworkTier">Which network tier the server is configured to be reachable at (Local, Network, or Internet).</param>
    /// <param name="InternetServerUrl">Internet-reachable URL (scheme + host + port) for paired clients to connect over the Internet, or null if not configured.</param>
    /// <param name="EnableLiveView">Toggles on-demand live view sessions for cameras.</param>
    /// <param name="LiveViewIdleTimeoutMinutes">Idle timeout for active (non-sustained) live view sessions in minutes.</param>
    /// <param name="LiveViewTelemetrySampleIntervalSeconds">Interval in seconds for capturing live view telemetry samples (RTCP, bitrate).</param>
    /// <param name="SustainedModeMaxDurationMinutes">Maximum duration in minutes for sustained-mode live view sessions; 0 = unlimited.</param>
    /// <param name="LiveViewTelemetryRetentionDays">Retention period in days for live view telemetry samples before automated pruning.</param>
    /// <param name="ElevatedPollingWindowMinutes">Duration in minutes of elevated-polling window when RSSI degradation is detected.</param>
    /// <param name="ElevatedPollingIntervalSeconds">Polling interval in seconds during elevated-polling windows for tiered device health checks.</param>
    /// <param name="EnableBitrateCalibration">Toggles automated bitrate calibration sessions to establish time-bucketed baselines.</param>
    /// <param name="CalibrationCheckIntervalMinutes">Check interval in minutes for launching calibration sessions for under-sampled or stale buckets.</param>
    /// <param name="CalibrationSessionDurationSeconds">Duration in seconds of a single bitrate calibration session.</param>
    /// <param name="MinCalibrationSamplesPerBucket">Minimum telemetry samples per time bucket to consider calibration complete for that bucket.</param>
    /// <param name="CalibrationStalenessDays">Staleness threshold in days; calibration buckets older than this are re-sampled even if at minimum threshold.</param>
    public record ClientConfigDto(
        bool EnableForensicAnalysisReports,
        bool EnableSignalAnomalyReports,
        bool EnableChainOfCustodyReports,
        bool EnableEvidenceValidationReports,
        bool EnableAccessControlMonitoring,
        bool EnablePiiRedaction,
        string ReportOutputFormat,
        RedactionLevel RedactionLevel,
        int RetentionDaysDefault,
        string LogLevel,
        int MaxConcurrentDownloads,
        string DownloadStartDate,
        Guid? ActiveProviderAccountId,
        bool EnableHealthSync,
        bool EnableMdnsAdvertisement,
        bool EnableEmailNotifications,
        NetworkTier ConfiguredNetworkTier,
        string? InternetServerUrl,
        bool EnableLiveView,
        int LiveViewIdleTimeoutMinutes,
        int LiveViewTelemetrySampleIntervalSeconds,
        int SustainedModeMaxDurationMinutes,
        int LiveViewTelemetryRetentionDays,
        int ElevatedPollingWindowMinutes,
        int ElevatedPollingIntervalSeconds,
        bool EnableBitrateCalibration,
        int CalibrationCheckIntervalMinutes,
        int CalibrationSessionDurationSeconds,
        int MinCalibrationSamplesPerBucket,
        int CalibrationStalenessDays
    );

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
                RedactionLevel: config.RedactionLevel,
                RetentionDaysDefault: config.RetentionDaysDefault,
                LogLevel: config.LogLevel,
                MaxConcurrentDownloads: config.MaxConcurrentDownloads,
                DownloadStartDate: config.DownloadStartDate,
                ActiveProviderAccountId: config.ActiveProviderAccountId,
                EnableHealthSync: config.EnableHealthSync,
                EnableMdnsAdvertisement: config.EnableMdnsAdvertisement,
                EnableEmailNotifications: config.EnableEmailNotifications,
                ConfiguredNetworkTier: config.ConfiguredNetworkTier,
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
    }
}
