namespace VideoForensics.Data.Common.Entities
{
    /// <summary>
    /// One download run bound to one provider account. Replaces the stored "active account" as the source of
    /// attribution for manual downloads.
    /// </summary>
    public class DownloadJob
    {
        /// <summary>Primary key.</summary>
        public Guid Id { get; set; }

        /// <summary>The provider account this job downloads for.</summary>
        public Guid ProviderAccountId { get; set; }

        /// <summary>Videos or snapshots.</summary>
        public DownloadJobKind Kind { get; set; }

        /// <summary>Start of the requested range (UTC).</summary>
        public DateTime StartDateUtc { get; set; }

        /// <summary>End of the requested range (UTC).</summary>
        public DateTime EndDateUtc { get; set; }

        /// <summary>When true, files already on disk are re-downloaded.</summary>
        public bool Force { get; set; }

        /// <summary>Current lifecycle state.</summary>
        public DownloadJobState State { get; set; }

        /// <summary>When the job was queued (UTC).</summary>
        public DateTime CreatedUtc { get; set; }

        /// <summary>When the runner claimed the job (UTC). Null while queued.</summary>
        public DateTime? StartedUtc { get; set; }

        /// <summary>When the job reached a terminal state (UTC).</summary>
        public DateTime? FinishedUtc { get; set; }

        /// <summary>Operator who requested the job, for audit.</summary>
        public Guid RequestedBy { get; set; }

        /// <summary>Files written during the run.</summary>
        public int FilesDownloaded { get; set; }

        /// <summary>Files skipped because they already exist.</summary>
        public int FilesSkipped { get; set; }

        /// <summary>Files that failed during the run.</summary>
        public int FilesFailed { get; set; }

        /// <summary>Operator-facing error summary. No stack traces (see csharp.md).</summary>
        public string? ErrorMessage { get; set; }
    }
}
