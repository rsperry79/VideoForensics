namespace VideoForensics.WebApp.Services
{
    /// <summary>
    /// Result of running full provider reconciliation with auto-fix across all devices.
    /// </summary>
    public class BulkValidationResult
    {
        public int TotalDevicesScanned { get; set; }
        public int TotalFilesVerified { get; set; }
        public int TotalFilesIntact { get; set; }
        public int TotalFilesFailed { get; set; }
        public int TotalFilesMissing { get; set; }
        public int TotalDiscrepanciesFound { get; set; }
        public int TotalDiscrepanciesFixed { get; set; }
        public DateTime StartedAtUtc { get; set; }
        public DateTime CompletedAtUtc { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
