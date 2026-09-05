namespace VideoForensics.Client.Common.Contracts
{
    /// <summary>Result of validating that every downloaded event's media/sidecar is present and correctly tagged, ahead of a backup export.</summary>
    public class PrepareExportResult
    {
        public int Validated { get; set; }
        public int Backfilled { get; set; }
        public int MissingMedia { get; set; }
        public int MissingSidecarUnrecoverable { get; set; }
        public List<string> Details { get; set; } = new();
    }

    /// <summary>Result of exporting the database to a JSON+zip backup archive.</summary>
    public class BackupExportResult
    {
        public bool Success { get; set; }
        public string? ArchivePath { get; set; }
        public string? ArchiveSha256Hash { get; set; }
        public int ProviderAccountCount { get; set; }
        public int LocationCount { get; set; }
        public int DeviceCount { get; set; }
        public int EventCount { get; set; }
        public int DownloadEventCount { get; set; }
        public int MediaItemCount { get; set; }
        public string? ErrorMessage { get; set; }
    }

    /// <summary>Service for validating downloaded evidence and exporting the database to a portable JSON+zip backup.</summary>
    public interface IBackupExportService
    {
        /// <summary>Validates every downloaded event has a matching sidecar/media-tag, backfilling where possible.</summary>
        Task<PrepareExportResult> PrepareForExportAsync(CancellationToken ct);

        /// <summary>Exports every entity table to JSON files (media paths relative to the configured media root) and zips them into a single backup archive.</summary>
        Task<BackupExportResult> ExportBackupAsync(string outputDirectory, CancellationToken ct);
    }
}
