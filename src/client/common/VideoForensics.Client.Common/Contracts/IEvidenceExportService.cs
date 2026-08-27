namespace VideoForensics.Client.Common
{
    /// <summary>Result of an evidence export operation.</summary>
    public class ExportResult
    {
        /// <summary>True if the export completed successfully (or partially with some items excluded).</summary>
        public bool Success { get; set; }

        /// <summary>Full path to the created archive file, if successful.</summary>
        public string? ArchivePath { get; set; }

        /// <summary>SHA-256 hash of the archive file, if successful.</summary>
        public string? ArchiveSha256Hash { get; set; }

        /// <summary>Number of media items included in the archive.</summary>
        public int ItemsIncluded { get; set; }

        /// <summary>Media item IDs that were excluded due to failed integrity verification.</summary>
        public IReadOnlyList<Guid> ItemsExcludedForFailedIntegrity { get; set; } = new List<Guid>();

        /// <summary>Error message if the export failed entirely, or null if successful.</summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>Service for orchestrating secure evidence exports into password-protected archives.</summary>
    public interface IEvidenceExportService
    {
        /// <summary>
        /// Exports selected media items into a password-protected ZIP archive.
        ///
        /// Process:
        /// 1. Verifies integrity of each selected item; excluded items that fail verification are flagged
        /// 2. Builds manifest.json with export metadata and per-item integrity status
        /// 3. Builds chain_of_custody.json from action log entries for each item
        /// 4. Creates a ZIP archive with media files + manifest + chain-of-custody entries, optionally encrypted with AES-256
        /// 5. Records the export in the database with per-item hashes at export time
        /// </summary>
        Task<ExportResult> ExportEvidenceAsync(
            IReadOnlyList<Guid> mediaItemIds,
            string outputDirectory,
            string? caseReference,
            string? recipientDescription,
            string? passphrase,
            CancellationToken ct);
    }
}
