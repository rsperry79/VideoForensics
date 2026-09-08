using VideoForensics.Data.Common.Entities;
using VideoForensics.Client.Common.Contracts;

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
        string? InternetServerUrl
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
                InternetServerUrl: config.InternetServerUrl
            );
        }
    }
}
