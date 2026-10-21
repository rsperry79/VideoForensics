namespace VideoForensics.Hosting.Contracts
{
    /// <summary>Outcome of one account's snapshot roll-up.</summary>
    /// <param name="DevicesAttempted">Devices of the account that were considered.</param>
    /// <param name="DevicesRolledUp">Devices that produced an MP4 loop for the window.</param>
    /// <param name="ErrorMessage">Null when every device either rolled up or had no stills; otherwise the failure summary.</param>
    public readonly record struct SnapshotRollupResult(int DevicesAttempted = 0, int DevicesRolledUp = 0, string? ErrorMessage = null);

    /// <summary>Turns one account's snapshots in a time window into one MP4 loop per device. The JPEGs are kept.</summary>
    public interface ISnapshotRollupService
    {
        /// <summary>Rolls up [fromUtc, toUtc) for every device of the account. Throws only on cancellation.</summary>
        Task<SnapshotRollupResult> RollUpAccountAsync(Guid accountId, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken);
    }
}
