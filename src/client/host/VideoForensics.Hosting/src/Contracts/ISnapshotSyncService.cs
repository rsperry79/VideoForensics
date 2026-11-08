using VideoForensics.Client.Hosting.Services;

namespace VideoForensics.Client.Hosting.Contracts
{
    /// <summary>What one snapshot sync did for an account.</summary>
    /// <param name="DevicesAttempted">Devices a snapshot was requested for.</param>
    /// <param name="DevicesSucceeded">Devices that returned a snapshot.</param>
    /// <param name="SnapshotsSaved">Snapshot files written.</param>
    /// <param name="DevicesFailed">Devices that failed or returned no snapshot.</param>
    /// <param name="ErrorMessage">Null when everything succeeded (or there was nothing to do); otherwise why the sync failed or partly failed.</param>
    public readonly record struct SnapshotSyncResult(
        int DevicesAttempted = 0,
        int DevicesSucceeded = 0,
        int SnapshotsSaved = 0,
        int DevicesFailed = 0,
        string? ErrorMessage = null);

    /// <summary>Fetches snapshots for all devices of one provider account.</summary>
    public interface ISnapshotSyncService
    {
        /// <summary>
        /// Fetches snapshots for every device of the account and saves them under the media storage root.
        /// The window is passed to the provider, but a provider that only exposes the current snapshot (Ring) ignores it
        /// and returns the latest one. Failures are reported in the result rather than thrown.
        /// </summary>
        /// <param name="accountId">The provider account.</param>
        /// <param name="fromUtc">Start of the requested window (UTC); null means unbounded.</param>
        /// <param name="toUtc">End of the requested window (UTC); null means now.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task<SnapshotSyncResult> SyncAccountSnapshotsAsync(Guid accountId, DateTime? fromUtc, DateTime? toUtc, CancellationToken cancellationToken);

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
