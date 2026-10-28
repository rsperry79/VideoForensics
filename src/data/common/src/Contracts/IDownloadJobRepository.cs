using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>
    /// Persists download jobs and hands them to the runner one at a time. Claiming is atomic, so two runners
    /// cannot take the same job.
    /// </summary>
    public interface IDownloadJobRepository
    {
        /// <summary>Adds a new job in the Queued state.</summary>
        Task EnqueueAsync(DownloadJob job, CancellationToken ct);

        /// <summary>Gets a job by ID, or null if none exists.</summary>
        Task<DownloadJob?> GetAsync(Guid id, CancellationToken ct);

        /// <summary>
        /// Atomically moves the oldest Queued job to Running and returns it, or null when none is queued.
        /// </summary>
        Task<DownloadJob?> ClaimNextQueuedAsync(CancellationToken ct);

        /// <summary>
        /// Marks a Running job Completed with its final counters. Returns false when the job is not Running any more
        /// (for example it was cancelled mid-run), so a late completion cannot overwrite that state.
        /// </summary>
        Task<bool> CompleteAsync(Guid id, int filesDownloaded, int filesSkipped, int filesFailed, CancellationToken ct);

        /// <summary>Marks a Running job Failed with an operator-facing message. Ignored unless the job is Running.</summary>
        Task FailAsync(Guid id, string errorMessage, CancellationToken ct);

        /// <summary>
        /// Cancels a Queued or Running job. Returns false when the job is already terminal or does not exist.
        /// </summary>
        Task<bool> CancelAsync(Guid id, CancellationToken ct);

        /// <summary>
        /// Moves every Running job back to Queued. Called once at startup, because a job that was Running when the
        /// process stopped has no live runner. Returns the number of jobs requeued.
        /// </summary>
        Task<int> RequeueRunningAsync(CancellationToken ct);

        /// <summary>Lists an account's jobs, newest CreatedUtc first, capped at <paramref name="take"/> (1 to 500).</summary>
        Task<IReadOnlyList<DownloadJob>> ListForAccountAsync(Guid accountId, int take, CancellationToken ct);
    }
}
