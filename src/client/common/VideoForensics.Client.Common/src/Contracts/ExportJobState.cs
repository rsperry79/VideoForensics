namespace VideoForensics.Client.Common.Contracts
{
    /// <summary>Lifecycle state of a background account export job.</summary>
    public enum ExportJobState
    {
        /// <summary>Waiting in the queue.</summary>
        Queued,

        /// <summary>Currently being exported.</summary>
        Running,

        /// <summary>Export file written (and account purged if requested).</summary>
        Succeeded,

        /// <summary>Export or purge failed; see the error message.</summary>
        Failed,
    }
}
