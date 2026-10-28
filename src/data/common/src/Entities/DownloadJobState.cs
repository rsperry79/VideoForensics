namespace VideoForensics.Data.Common.Entities
{
    /// <summary>Lifecycle of a download job. Stored as text.</summary>
    public enum DownloadJobState
    {
        /// <summary>Waiting for the runner.</summary>
        Queued = 0,

        /// <summary>Claimed by the runner.</summary>
        Running = 1,

        /// <summary>Finished without a fatal error.</summary>
        Completed = 2,

        /// <summary>Stopped by a fatal error; see ErrorMessage.</summary>
        Failed = 3,

        /// <summary>Cancelled before or during the run.</summary>
        Cancelled = 4,
    }
}
