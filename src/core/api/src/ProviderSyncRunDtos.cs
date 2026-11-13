namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>
    /// Row for the account details page's sync history list.
    /// </summary>
    /// <param name="Id">Sync run ID.</param>
    /// <param name="StartedUtc">UTC time the sync started.</param>
    /// <param name="CompletedUtc">UTC time the sync finished; null when the run did not complete.</param>
    /// <param name="Success">True when the sync finished without an account error.</param>
    /// <param name="SyncType">"Full" or "Incremental"; null for runs recorded before this was tracked.</param>
    /// <param name="DevicesSynced">Devices stored by the run; null when not recorded.</param>
    /// <param name="LocationsSynced">Locations stored by the run; null when not recorded.</param>
    /// <param name="EventsSynced">Events stored by the run; null when not recorded.</param>
    /// <param name="ConfigAreasSynced">Configuration areas stored by the run; null when not recorded.</param>
    /// <param name="FeedsFailed">Per-device event feeds that failed; null when not recorded.</param>
    /// <param name="SnapshotsSynced">Devices that returned a snapshot; null when snapshots were not part of the run.</param>
    public record ProviderSyncRunSummaryDto(
        Guid Id,
        DateTime StartedUtc,
        DateTime? CompletedUtc,
        bool Success,
        string? SyncType = null,
        int? DevicesSynced = null,
        int? LocationsSynced = null,
        int? EventsSynced = null,
        int? ConfigAreasSynced = null,
        int? FeedsFailed = null,
        int? SnapshotsSynced = null
    );

    /// <summary>
    /// Full detail of a single account sync run, including the failure message for unsuccessful runs.
    /// </summary>
    /// <param name="Id">Sync run ID.</param>
    /// <param name="ProviderAccountId">The provider account this sync ran for.</param>
    /// <param name="StartedUtc">UTC time the sync started.</param>
    /// <param name="CompletedUtc">UTC time the sync finished; null when the run did not complete.</param>
    /// <param name="Success">True when the sync finished without an account error.</param>
    /// <param name="ErrorMessage">Failure message for an unsuccessful run; null when Success is true.</param>
    public record ProviderSyncRunDetailDto(
        Guid Id,
        Guid ProviderAccountId,
        DateTime StartedUtc,
        DateTime? CompletedUtc,
        bool Success,
        string? ErrorMessage
    );
}
