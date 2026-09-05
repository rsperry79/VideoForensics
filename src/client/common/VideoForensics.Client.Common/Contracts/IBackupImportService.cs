namespace VideoForensics.Client.Common.Contracts
{
    /// <summary>Per-entity-type counts from a backup import stage.</summary>
    public class BackupImportStageResult
    {
        public int Inserted { get; set; }
        public int SkippedExisting { get; set; }
        public int SkippedOrphaned { get; set; }
        public int IntegrityIssues { get; set; }
    }

    /// <summary>Result of importing a backup archive back into the database.</summary>
    public class BackupImportResult
    {
        public bool Success { get; set; }
        public BackupImportStageResult ProviderAccounts { get; set; } = new();
        public BackupImportStageResult Locations { get; set; } = new();
        public BackupImportStageResult Devices { get; set; } = new();
        public BackupImportStageResult Events { get; set; } = new();
        public BackupImportStageResult DownloadEvents { get; set; } = new();
        public BackupImportStageResult MediaItems { get; set; } = new();
        public List<string> Details { get; set; } = new();
        public string? ErrorMessage { get; set; }
    }

    /// <summary>Service for reimporting a JSON+zip backup archive produced by IBackupExportService, skipping orphaned/duplicate records.</summary>
    public interface IBackupImportService
    {
        /// <summary>
        /// Imports a backup zip's accounts/locations/devices/events/media-items into the database, in
        /// dependency order, skipping any record whose parent isn't present (pre-existing or already
        /// imported this run) and skipping any record whose ID already exists rather than overwriting it.
        /// Media item file paths are rehomed under mediaRootPath and their SHA-256 hash is re-verified.
        /// </summary>
        Task<BackupImportResult> ImportBackupAsync(string backupZipPath, string mediaRootPath, CancellationToken ct);
    }
}
