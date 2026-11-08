using VideoForensics.Client.Hosting.Services;

namespace VideoForensics.Client.Hosting.Contracts
{
    /// <summary>Runs the snapshot capture cycle for the Ring accounts.</summary>
    public interface ISnapshotSyncService
    {
        /// <summary>
        /// Runs one snapshot capture pass for every active Ring account, through <see cref="SnapshotCycle"/>: each enabled camera
        /// whose window has no attempt is captured. Called once per tick by the snapshot sync background service.
        /// A failing account is recorded in the returned counts and does not stop the others; cancellation is thrown.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>The tick's outcome counts, including whether any account's pass threw.</returns>
        Task<SnapshotCycle.TickCounts> RunCycleAsync(CancellationToken cancellationToken);
    }
}
