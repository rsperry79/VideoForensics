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
    }
}
