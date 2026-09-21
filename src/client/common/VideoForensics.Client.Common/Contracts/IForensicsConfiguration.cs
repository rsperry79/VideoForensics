using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.Common.Contracts
{
    public interface IForensicsConfiguration
    {
        bool EnableForensicAnalysisReports { get; set; }
        bool EnableSignalAnomalyReports { get; set; }
        bool EnableChainOfCustodyReports { get; set; }
        bool EnableEvidenceValidationReports { get; set; }
        bool EnableAccessControlMonitoring { get; set; }
        bool EnablePiiRedaction { get; set; }
        string ReportOutputFormat { get; set; }
        string? DownloadLocation { get; set; }
        string? QueryExportLocation { get; set; }
        string? DatabaseLocation { get; set; }
        string? TempDownloadLocation { get; set; }
        string? LogsLocation { get; set; }
        string? ReportsLocation { get; set; }
        RedactionLevel RedactionLevel { get; set; }
        KeyStorageProvider KeyStorageProvider { get; set; }
        int RetentionDaysDefault { get; set; }
        string LogLevel { get; set; }
        int MaxConcurrentDownloads { get; set; }
        /// <summary>Start date for downloads, as a "yyyy-MM-dd" string parsed to a DateTime at use. Empty means unset.</summary>
        string DownloadStartDate { get; set; }
        Guid? ActiveProviderAccountId { get; set; }
        /// <summary>Toggles the periodic RSSI/device-health background sync (DeviceHealthSyncService). Default on.</summary>
        bool EnableHealthSync { get; set; }
        /// <summary>Toggles LAN mDNS advertisement of this server (_videoforensics._tcp.local, plan §5.2) so a pairing client can find it without typing an IP. Default on. Only meaningful on VideoForensics.WebApp - console/MCP have no pairing API to advertise.</summary>
        bool EnableMdnsAdvertisement { get; set; }

        /// <summary>Toggles the periodic update-check background service. Default on.</summary>
        bool EnableUpdateCheck { get; set; }
        /// <summary>Whether an available update is only surfaced to the UI (NotifyOnly) or automatically downloaded and the installer launched (AutoDownloadAndInstall). Default is NotifyOnly.</summary>
        UpdateCheckMode UpdateMode { get; set; }
        /// <summary>Polling interval in hours for the update-check background service. GitHub's unauthenticated REST API is rate-limited to 60 requests/hour, so this must stay well above "every few minutes". Default is 24.</summary>
        int UpdateCheckIntervalHours { get; set; }
        /// <summary>Selects whether the update-check tracks tagged Stable releases or the rolling Dev prerelease build. Default is Stable.</summary>
        UpdateReleaseChannel ReleaseChannel { get; set; }

        /// <summary>Toggles the email notification channel for urgent security events (plan §5.6). The SMTP password itself is NOT stored here - see ISmtpPasswordStore, which routes it through ICredentialEncryptionProvider per plan §4.1.</summary>
        bool EnableEmailNotifications { get; set; }
        string SmtpHost { get; set; }
        int SmtpPort { get; set; }
        bool SmtpUseTls { get; set; }
        string SmtpUsername { get; set; }
        string SmtpFromAddress { get; set; }
        /// <summary>Where urgent security notifications are sent - the server owner's own inbox, not tied to any Operator record.</summary>
        string NotificationRecipientEmail { get; set; }

        /// <summary>
        /// Which network tier the server is configured to be reachable at (plan §5.2) - Local-only
        /// by default, each wider tier an explicit opt-in. Reuses NetworkTier, the SAME enum
        /// INetworkTierResolver uses to classify an individual incoming request, per the plan's own
        /// instruction not to invent a second concept. Read once at WebApp startup (before the host
        /// is built - see Program.cs) to decide which interfaces Kestrel actually binds to; changing
        /// it here takes effect only after a server restart, since a listen socket can't be rebound
        /// live.
        /// </summary>
        NetworkTier ConfiguredNetworkTier { get; set; }

        /// <summary>
        /// Internet-reachable URL (scheme + host + port) that paired clients may use to connect
        /// to this server over the Internet. Optional; if not set, clients cannot fall back to
        /// Internet access if LAN discovery fails. SuperAdmin-only setting.
        /// </summary>
        string? InternetServerUrl { get; set; }

        /// <summary>
        /// Uniview NVR host address (IP address or hostname) for local-network downloads.
        /// Only used by the Uniview provider for connecting to a local Uniview NVR device.
        /// Optional; if not set, Uniview downloads will fail.
        /// </summary>
        string? UniviewNvrHost { get; set; }

        /// <summary>
        /// Path to the ffmpeg executable, used by the Uniview provider for video remuxing and snapshot capture.
        /// Defaults to "ffmpeg" (assumes it's in PATH). Can be an absolute path to a specific ffmpeg binary.
        /// </summary>
        string? UniviewFfmpegPath { get; set; }
    }

    public enum RedactionLevel
    {
        None,
        Light,
        Medium,
        Heavy
    }

    public enum KeyStorageProvider
    {
        Auto,
        Tpm,
        Dpapi,
        FileBased
    }

    public enum UpdateCheckMode
    {
        NotifyOnly,
        AutoDownloadAndInstall
    }

    public enum UpdateReleaseChannel
    {
        Stable,
        Testing
    }

    public class ForensicsConfiguration : IForensicsConfiguration
    {
        public bool EnableForensicAnalysisReports { get; set; } = true;
        public bool EnableSignalAnomalyReports { get; set; } = true;
        public bool EnableChainOfCustodyReports { get; set; } = true;
        public bool EnableEvidenceValidationReports { get; set; } = true;
        public bool EnableAccessControlMonitoring { get; set; } = true;
        public bool EnablePiiRedaction { get; set; } = true;
        public string ReportOutputFormat { get; set; } = "json";
        public string? DownloadLocation { get; set; }
        public string? QueryExportLocation { get; set; }
        public string? DatabaseLocation { get; set; }
        public string? TempDownloadLocation { get; set; }
        public string? LogsLocation { get; set; }
        public string? ReportsLocation { get; set; }
        public RedactionLevel RedactionLevel { get; set; } = RedactionLevel.Medium;
        public KeyStorageProvider KeyStorageProvider { get; set; } = KeyStorageProvider.Auto;
        public int RetentionDaysDefault { get; set; } = 180;
        public string LogLevel { get; set; } = "Information";
        public int MaxConcurrentDownloads { get; set; } = 10;
        public string DownloadStartDate { get; set; } = "";
        public Guid? ActiveProviderAccountId { get; set; }
        public bool EnableHealthSync { get; set; } = true;
        public bool EnableMdnsAdvertisement { get; set; } = true;
        public bool EnableUpdateCheck { get; set; } = true;
        public UpdateCheckMode UpdateMode { get; set; } = UpdateCheckMode.NotifyOnly;
        public int UpdateCheckIntervalHours { get; set; } = 24;
        public UpdateReleaseChannel ReleaseChannel { get; set; } = UpdateReleaseChannel.Stable;
        public bool EnableEmailNotifications { get; set; } = false;
        public string SmtpHost { get; set; } = "";
        public int SmtpPort { get; set; } = 587;
        public bool SmtpUseTls { get; set; } = true;
        public string SmtpUsername { get; set; } = "";
        public string SmtpFromAddress { get; set; } = "";
        public string NotificationRecipientEmail { get; set; } = "";
        public NetworkTier ConfiguredNetworkTier { get; set; } = NetworkTier.Local;
        public string? InternetServerUrl { get; set; }
        public string? UniviewNvrHost { get; set; }
        public string? UniviewFfmpegPath { get; set; }
    }
}
