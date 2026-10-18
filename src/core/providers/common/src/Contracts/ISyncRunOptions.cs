namespace VideoForensics.Core.Providers.Common.Contracts
{
    /// <summary>
    /// Which feeds a manual sync runs. <see cref="Default"/> is the legacy sync: config under its interval gate, then events.
    /// </summary>
    public enum SyncRunKind
    {
        /// <summary>Legacy sync: config runs when its interval has elapsed, then the event feeds run.</summary>
        Default,

        /// <summary>Config fetches run regardless of the interval gate; event feeds are skipped.</summary>
        Config,

        /// <summary>Event feeds run for the range; config is skipped.</summary>
        Events,

        /// <summary>Snapshot and device-health capture only; config and event feeds are skipped.</summary>
        Snapshots,
    }

    /// <summary>How the window for a sync is chosen.</summary>
    public enum SyncRangeKind
    {
        /// <summary>From the account's last successful sync, or 30 days back when there is none.</summary>
        SinceLast,

        /// <summary>From now minus <see cref="SyncRunOptions.Days"/>.</summary>
        Days,

        /// <summary>From <see cref="SyncRunOptions.StartUtc"/> to <see cref="SyncRunOptions.EndUtc"/>.</summary>
        Custom,

        /// <summary>The provider's whole retained history, up to the one-year initial-backfill horizon.</summary>
        All,
    }

    /// <summary>
    /// The sync kind and window for one manual sync. The runner resolves the window and publishes the result through
    /// <see cref="ISyncRunOptions"/> so providers can bound their history reads without changing their contracts.
    /// </summary>
    /// <param name="Kind">Which feeds run.</param>
    /// <param name="RangeKind">How the window is chosen.</param>
    /// <param name="Days">Days back from now. Used when <paramref name="RangeKind"/> is Days.</param>
    /// <param name="StartUtc">Window start. Null leaves the provider's default (full history) in place.</param>
    /// <param name="EndUtc">Window end. Null means now.</param>
    /// <param name="IncludeSnapshots">True when a Default-kind sync should also fetch snapshots after the pull (the "All" sync type). Ignored by other kinds.</param>
    public sealed record SyncRunOptions(
        SyncRunKind Kind = SyncRunKind.Default,
        SyncRangeKind RangeKind = SyncRangeKind.SinceLast,
        int? Days = null,
        DateTime? StartUtc = null,
        DateTime? EndUtc = null,
        bool IncludeSnapshots = false);

    /// <summary>
    /// The options of the sync running in this DI scope. Scoped, like <see cref="ISyncRunTally"/>: the runner sets it before
    /// the pull, and providers read it. A missing registration means the legacy sync.
    /// </summary>
    public interface ISyncRunOptions
    {
        /// <summary>The options in effect for this scope's sync.</summary>
        SyncRunOptions Current { get; set; }
    }
}
