namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>
    /// Data transfer object for a user account.
    /// </summary>
    /// <param name="Id">Unique identifier for the user.</param>
    /// <param name="ProviderUserKey">The provider's unique identifier for this user.</param>
    /// <param name="DisplayName">User-friendly display name of the user.</param>
    /// <param name="Email">User's email address, if available.</param>
    /// <param name="CreatedUtc">Timestamp when the user account was created, in UTC.</param>
    public record UserDto(
        Guid Id,
        string ProviderUserKey,
        string DisplayName,
        string? Email,
        DateTime CreatedUtc
    );

    /// <summary>
    /// Data transfer object for a provider account link.
    /// Represents the association between a user and a provider (e.g., Ring, Wyze).
    /// </summary>
    /// <param name="Id">Unique identifier for the provider account link.</param>
    /// <param name="UserId">The user this provider account is linked to.</param>
    /// <param name="ProviderName">Name of the provider (e.g., "Ring", "Wyze").</param>
    /// <param name="LinkedUtc">Timestamp when the account was linked, in UTC.</param>
    /// <param name="LastSuccessfulAuthUtc">Timestamp of the last successful authentication with the provider, in UTC. Null if never authenticated.</param>
    /// <param name="IsActive">True if this provider account is active and can be used for downloads and operations; false if deactivated.</param>
    /// <param name="LastDownloadTimeUtc">Timestamp of the last batch download completion for this account, used as the default start time for the next download. Null if never downloaded.</param>
    /// <param name="LastErrorUtc">Timestamp of the most recent failure surfaced to operators (e.g. credential decryption failure). Null if no error or cleared on next successful operation.</param>
    /// <param name="LastErrorMessage">Description of the most recent failure surfaced to operators (e.g. credential decryption failure). Null if no error or cleared on next successful operation.</param>
    /// <param name="RemovedUtc">When the account was retired (soft-removed); null while it is active.</param>
    public record ProviderAccountDto(
        Guid Id,
        Guid UserId,
        string ProviderName,
        DateTime LinkedUtc,
        DateTime? LastSuccessfulAuthUtc,
        bool IsActive,
        DateTime? LastDownloadTimeUtc,
        DateTime? LastErrorUtc,
        string? LastErrorMessage,
        DateTime? RemovedUtc = null
    );

    /// <summary>Request body for queueing a background export of a provider account.</summary>
    /// <param name="PurgeAfterExport">When true, the account is hard-deleted after the export file is written.</param>
    /// <param name="ConfirmPurge">The operator confirmed the purge in the UI. Required whenever <paramref name="PurgeAfterExport"/> is true.</param>
    public record QueueAccountExportRequest(bool PurgeAfterExport, bool ConfirmPurge = false)
    {
        /// <summary>Returned when a purge is requested without the operator's confirmation.</summary>
        public const string PurgeNotConfirmedMessage = "Purging an account needs the operator to confirm it first.";

        /// <summary>The reason this request is refused, or null when it may proceed.</summary>
        public string? ValidationError => PurgeAfterExport && !ConfirmPurge ? PurgeNotConfirmedMessage : null;
    }

    /// <summary>Wire form of a background account export job's status.</summary>
    /// <param name="JobId">The job id returned when the export was queued.</param>
    /// <param name="AccountId">The provider account being exported.</param>
    /// <param name="State">Lifecycle state name (Queued, Running, Succeeded, Failed).</param>
    /// <param name="FilePath">Final export file path once written; null before that.</param>
    /// <param name="ErrorMessage">Failure reason when the state is Failed.</param>
    /// <param name="PurgeAfterExport">Whether the account is hard-deleted after a successful export.</param>
    /// <param name="CreatedUtc">When the job was queued.</param>
    /// <param name="CompletedUtc">When the job reached Succeeded or Failed.</param>
    public record ExportJobDto(
        Guid JobId,
        Guid AccountId,
        string State,
        string? FilePath,
        string? ErrorMessage,
        bool PurgeAfterExport,
        DateTime CreatedUtc,
        DateTime? CompletedUtc
    );

    /// <summary>
    /// Response from POST /api/v1/accounts/{accountId}/sync-now endpoint.
    /// Indicates sync operation was queued as a background task.
    /// </summary>
    /// <param name="Success">True if sync was successfully queued.</param>
    /// <param name="Message">Human-readable message (e.g., "Sync started" or error description).</param>
    /// <param name="SyncStartedAtUtc">UTC timestamp when sync operation started.</param>
    public record SyncNowResponseDto(
        bool Success,
        string Message,
        DateTime SyncStartedAtUtc
    );

    /// <summary>
    /// Request body for POST /api/v1/accounts/provider-accounts/{id}/sync-now. A missing body runs the legacy sync.
    /// Null fields default to SyncType "Events" and RangeKind "SinceLast".
    /// </summary>
    /// <param name="SyncType">"All" (events and device configuration) or "Events".</param>
    /// <param name="RangeKind">"SinceLast", "Days", "Custom" or "All" (the provider's retained history).</param>
    /// <param name="Days">Days back from now, 1 to 3650. Required when RangeKind is "Days".</param>
    /// <param name="StartUtc">Range start, required for "Custom". Must be before EndUtc.</param>
    /// <param name="EndUtc">Range end, required for "Custom".</param>
    public record SyncNowRequest(
        string? SyncType,
        string? RangeKind,
        int? Days,
        DateTime? StartUtc,
        DateTime? EndUtc
    );

    /// <summary>
    /// The account's config sync interval in hours. Returned by GET and accepted by PUT
    /// /api/v1/accounts/provider-accounts/{id}/config-sync-interval; PUT accepts 1 to 720.
    /// </summary>
    /// <param name="Hours">Hours between config syncs.</param>
    public record ConfigSyncIntervalDto(int Hours);

    /// <summary>
    /// Tracks polling intervals for events and snapshots/RSSI data, with optional advanced jamming detection scheduling.
    /// </summary>
    /// <param name="Id">Unique identifier for the sync schedule.</param>
    /// <param name="ProviderAccountId">The provider account this schedule applies to.</param>
    /// <param name="EventPollIntervalMinutes">Interval in minutes for pulling events (rule violations, motion, person detection).</param>
    /// <param name="SnapshotRssiIntervalSeconds">Interval in seconds for pulling snapshots and RSSI data (default when no jamming window applies).</param>
    /// <param name="IsEnabled">Indicates whether syncing is enabled for this account.</param>
    /// <param name="UseAdvancedJammingSchedule">Indicates whether the advanced jamming detection schedule is active.</param>
    /// <param name="EventNextRunUtc">The UTC timestamp when the next event poll is scheduled to run; null if no future poll is scheduled.</param>
    /// <param name="EventLastRunUtc">The UTC timestamp of the most recent event poll run; null if never run.</param>
    public record SyncScheduleDto(
        Guid Id,
        Guid ProviderAccountId,
        int EventPollIntervalMinutes,
        int SnapshotRssiIntervalSeconds,
        bool IsEnabled,
        bool UseAdvancedJammingSchedule,
        DateTime? EventNextRunUtc,
        DateTime? EventLastRunUtc
    );
}
