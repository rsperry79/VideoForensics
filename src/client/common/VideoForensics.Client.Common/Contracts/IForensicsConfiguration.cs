using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.Common
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
        public RedactionLevel RedactionLevel { get; set; } = RedactionLevel.Medium;
        public KeyStorageProvider KeyStorageProvider { get; set; } = KeyStorageProvider.Auto;
        public int RetentionDaysDefault { get; set; } = 180;
        public string LogLevel { get; set; } = "Information";
        public int MaxConcurrentDownloads { get; set; } = 10;
        public string DownloadStartDate { get; set; } = "";
        public Guid? ActiveProviderAccountId { get; set; }
        public bool EnableHealthSync { get; set; } = true;
        public bool EnableMdnsAdvertisement { get; set; } = true;
        public bool EnableEmailNotifications { get; set; } = false;
        public string SmtpHost { get; set; } = "";
        public int SmtpPort { get; set; } = 587;
        public bool SmtpUseTls { get; set; } = true;
        public string SmtpUsername { get; set; } = "";
        public string SmtpFromAddress { get; set; } = "";
        public string NotificationRecipientEmail { get; set; } = "";
        public NetworkTier ConfiguredNetworkTier { get; set; } = NetworkTier.Local;
    }
}
