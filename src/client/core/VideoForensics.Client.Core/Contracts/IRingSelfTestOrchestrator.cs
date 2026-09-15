using VideoForensics.Api.Contracts;
using VideoForensics.Providers.Ring;

namespace VideoForensics.Client.Core.Contracts
{
    /// <summary>
    /// Orchestrates Ring self-test runs, managing their lifecycle and state.
    /// </summary>
    public interface IRingSelfTestOrchestrator
    {
        /// <summary>
        /// Attempts to start a self-test run. Returns false if a run is already in progress.
        /// On success, fires the run asynchronously via Task.Run (fire-and-forget from the caller's perspective).
        /// </summary>
        bool TryStartRun(RunOptions options, string outputDir);

        /// <summary>
        /// Gets the current run status and timing information.
        /// </summary>
        (SelfTestRunStatus Status, DateTime? StartedAtUtc, DateTime? CompletedAtUtc, string? Error) GetStatus();

        /// <summary>
        /// Gets the result of the last completed run, or null if no run has completed yet.
        /// </summary>
        IndexDocument? GetResult();
    }
}
