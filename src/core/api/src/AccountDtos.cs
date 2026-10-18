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
    public record QueueAccountExportRequest(bool PurgeAfterExport);

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

    /// <summary>Extension methods for mapping export job statuses to/from <see cref="ExportJobDto"/>.</summary>
    public static class ExportJobDtoMapping
    {
        /// <summary>Converts an export job status to its wire DTO.</summary>
        public static ExportJobDto ToDto(this VideoForensics.Client.Common.Contracts.ExportJobStatus entity)
        {
            return new ExportJobDto(entity.JobId, entity.AccountId, entity.State.ToString(), entity.FilePath, entity.ErrorMessage, entity.PurgeAfterExport, entity.CreatedUtc, entity.CompletedUtc);
        }

        /// <summary>Converts an export job DTO back to the domain status.</summary>
        public static VideoForensics.Client.Common.Contracts.ExportJobStatus ToDomain(this ExportJobDto dto)
        {
            return new VideoForensics.Client.Common.Contracts.ExportJobStatus(
                dto.JobId,
                dto.AccountId,
                Enum.TryParse(dto.State, true, out VideoForensics.Client.Common.Contracts.ExportJobState state) ? state : VideoForensics.Client.Common.Contracts.ExportJobState.Failed,
                dto.FilePath,
                dto.ErrorMessage,
                dto.PurgeAfterExport,
                dto.CreatedUtc,
                dto.CompletedUtc);
        }
    }

    /// <summary>Extension methods for mapping User entities to/from UserDtos.</summary>
    public static class UserDtoMapping
    {
        /// <summary>
        /// Converts a User entity to a UserDto.
        /// </summary>
        public static UserDto ToDto(this VideoForensics.Data.Common.Entities.User entity)
        {
            return new UserDto(
                Id: entity.Id,
                ProviderUserKey: entity.ProviderUserKey,
                DisplayName: entity.DisplayName,
                Email: entity.Email,
                CreatedUtc: entity.CreatedUtc
            );
        }

        /// <summary>
        /// Converts a UserDto to a User entity.
        /// </summary>
        public static VideoForensics.Data.Common.Entities.User ToDomain(this UserDto dto)
        {
            return new VideoForensics.Data.Common.Entities.User
            {
                Id = dto.Id,
                ProviderUserKey = dto.ProviderUserKey,
                DisplayName = dto.DisplayName,
                Email = dto.Email,
                CreatedUtc = dto.CreatedUtc
            };
        }
    }

    /// <summary>Extension methods for mapping ProviderAccount entities to/from ProviderAccountDtos.</summary>
    public static class ProviderAccountDtoMapping
    {
        /// <summary>
        /// Converts a ProviderAccount entity to a ProviderAccountDto.
        /// </summary>
        public static ProviderAccountDto ToDto(this VideoForensics.Data.Common.Entities.ProviderAccount entity)
        {
            return new ProviderAccountDto(
                Id: entity.Id,
                UserId: entity.UserId,
                ProviderName: entity.ProviderName,
                LinkedUtc: entity.LinkedUtc,
                LastSuccessfulAuthUtc: entity.LastSuccessfulAuthUtc,
                IsActive: entity.IsActive,
                LastDownloadTimeUtc: entity.LastDownloadTimeUtc,
                LastErrorUtc: entity.LastErrorUtc,
                LastErrorMessage: entity.LastErrorMessage,
                RemovedUtc: entity.RemovedUtc
            );
        }

        /// <summary>
        /// Converts a ProviderAccountDto to a ProviderAccount entity.
        /// </summary>
        public static VideoForensics.Data.Common.Entities.ProviderAccount ToDomain(this ProviderAccountDto dto)
        {
            return new VideoForensics.Data.Common.Entities.ProviderAccount
            {
                Id = dto.Id,
                UserId = dto.UserId,
                ProviderName = dto.ProviderName,
                LinkedUtc = dto.LinkedUtc,
                LastSuccessfulAuthUtc = dto.LastSuccessfulAuthUtc,
                IsActive = dto.IsActive,
                LastDownloadTimeUtc = dto.LastDownloadTimeUtc,
                LastErrorUtc = dto.LastErrorUtc,
                LastErrorMessage = dto.LastErrorMessage,
                RemovedUtc = dto.RemovedUtc
            };
        }
    }

    /// <summary>
    /// Data transfer object for a sync schedule.
    /// Tracks polling intervals for events and snapshots/RSSI data, with optional advanced jamming detection scheduling.
    /// </summary>
    /// <param name="Id">Unique identifier for the sync schedule.</param>
    /// <param name="ProviderAccountId">The provider account this schedule applies to.</param>
    /// <param name="EventPollIntervalMinutes">Interval in minutes for pulling events (rule violations, motion, person detection).</param>
    /// <param name="SnapshotRssiIntervalMinutes">Interval in minutes for pulling snapshots and RSSI data (default when no jamming window applies).</param>
    /// <param name="IsEnabled">Indicates whether syncing is enabled for this account.</param>
    /// <param name="UseAdvancedJammingSchedule">Indicates whether the advanced jamming detection schedule is active.</param>
    /// <param name="EventNextRunUtc">The UTC timestamp when the next event poll is scheduled to run; null if no future poll is scheduled.</param>
    /// <param name="EventLastRunUtc">The UTC timestamp of the most recent event poll run; null if never run.</param>
    /// <param name="SnapshotNextRunUtc">The UTC timestamp when the next snapshot/RSSI poll is scheduled to run; null if no future poll is scheduled.</param>
    /// <param name="SnapshotLastRunUtc">The UTC timestamp of the most recent snapshot/RSSI poll run; null if never run.</param>
    public record SyncScheduleDto(
        Guid Id,
        Guid ProviderAccountId,
        int EventPollIntervalMinutes,
        int SnapshotRssiIntervalMinutes,
        bool IsEnabled,
        bool UseAdvancedJammingSchedule,
        DateTime? EventNextRunUtc,
        DateTime? EventLastRunUtc,
        DateTime? SnapshotNextRunUtc,
        DateTime? SnapshotLastRunUtc
    );

    /// <summary>Extension methods for mapping SyncSchedule entities to/from SyncScheduleDtos.</summary>
    public static class SyncScheduleDtoMapping
    {
        /// <summary>
        /// Converts a SyncSchedule entity to a SyncScheduleDto.
        /// </summary>
        public static SyncScheduleDto ToDto(this VideoForensics.Data.Common.Entities.SyncSchedule entity)
        {
            return new SyncScheduleDto(
                Id: entity.Id,
                ProviderAccountId: entity.ProviderAccountId,
                EventPollIntervalMinutes: entity.EventPollIntervalMinutes,
                SnapshotRssiIntervalMinutes: entity.SnapshotRssiIntervalMinutes,
                IsEnabled: entity.IsEnabled,
                UseAdvancedJammingSchedule: entity.UseAdvancedJammingSchedule,
                EventNextRunUtc: entity.EventNextRunUtc,
                EventLastRunUtc: entity.EventLastRunUtc,
                SnapshotNextRunUtc: entity.SnapshotNextRunUtc,
                SnapshotLastRunUtc: entity.SnapshotLastRunUtc
            );
        }

        /// <summary>
        /// Converts a SyncScheduleDto to a SyncSchedule entity.
        /// </summary>
        public static VideoForensics.Data.Common.Entities.SyncSchedule ToDomain(this SyncScheduleDto dto)
        {
            return new VideoForensics.Data.Common.Entities.SyncSchedule
            {
                Id = dto.Id,
                ProviderAccountId = dto.ProviderAccountId,
                EventPollIntervalMinutes = dto.EventPollIntervalMinutes,
                SnapshotRssiIntervalMinutes = dto.SnapshotRssiIntervalMinutes,
                IsEnabled = dto.IsEnabled,
                UseAdvancedJammingSchedule = dto.UseAdvancedJammingSchedule,
                EventNextRunUtc = dto.EventNextRunUtc,
                EventLastRunUtc = dto.EventLastRunUtc,
                SnapshotNextRunUtc = dto.SnapshotNextRunUtc,
                SnapshotLastRunUtc = dto.SnapshotLastRunUtc
            };
        }
    }

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
    /// <param name="SyncType">"All" (events and snapshots), "Config", "Snapshots" or "Events".</param>
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
}
