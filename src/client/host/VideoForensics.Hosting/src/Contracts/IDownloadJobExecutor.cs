using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.Hosting.Contracts
{
    /// <summary>Result of running one download job. <see cref="ErrorMessage"/> set means the job failed.</summary>
    /// <param name="FilesDownloaded">Files written during the run.</param>
    /// <param name="FilesSkipped">Files skipped because they already exist.</param>
    /// <param name="FilesFailed">Files or devices that failed during the run.</param>
    /// <param name="ErrorMessage">Operator-facing reason the job failed; null when it completed.</param>
    public sealed record DownloadJobOutcome(int FilesDownloaded, int FilesSkipped, int FilesFailed, string? ErrorMessage);

    /// <summary>Runs one claimed download job for its provider account.</summary>
    public interface IDownloadJobExecutor
    {
        /// <summary>
        /// Runs the job against its account. Returns an outcome rather than throwing for expected failures
        /// (account gone, credentials missing). Unexpected exceptions propagate to the runner.
        /// </summary>
        Task<DownloadJobOutcome> ExecuteAsync(DownloadJob job, CancellationToken ct);
    }
}
