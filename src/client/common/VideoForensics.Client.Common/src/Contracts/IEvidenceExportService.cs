namespace VideoForensics.Client.Common.Contracts
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
        public IReadOnlyList<Guid> ItemsExcludedForFailedIntegrity { get; set; } = [];

        /// <summary>Error message if the export failed entirely, or null if successful.</summary>
        public string? ErrorMessage { get; set; }
    }

    /// <summary>Service for orchestrating secure evidence exports into password-protected archives.</summary>
    public interface IEvidenceExportService
    {
        /// <summary>
        /// Exports one case's forensic reports and pinned evidence (media) into a ZIP archive.
        /// The export is always scoped to <paramref name="caseId"/>; it never includes another case's items.
        ///
        /// Process:
        /// 1. Resolves the case and its active (not soft-removed) pinned media items
        /// 2. Verifies integrity of each item; items that fail verification are excluded and flagged
        /// 3. Builds the case's reports (per scope device over the case time window) under reports/
        /// 4. Builds manifest.json and chain_of_custody.json
        /// 5. Creates the ZIP archive, optionally encrypted with AES-256, and records the export with per-item hashes
        /// The case number is used as the case reference. The archive is written under the exports location stored
        /// in the database; callers cannot choose the destination.
        /// </summary>
        Task<ExportResult> ExportEvidenceAsync(
            Guid caseId,
            string? recipientDescription,
            string? passphrase,
            CancellationToken ct,
            string? stepUpToken = null);
    }
}
