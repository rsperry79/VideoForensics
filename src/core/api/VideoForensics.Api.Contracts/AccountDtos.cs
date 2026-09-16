namespace VideoForensics.Api.Contracts
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
    public record ProviderAccountDto(
        Guid Id,
        Guid UserId,
        string ProviderName,
        DateTime LinkedUtc,
        DateTime? LastSuccessfulAuthUtc,
        bool IsActive,
        DateTime? LastDownloadTimeUtc,
        DateTime? LastErrorUtc,
        string? LastErrorMessage
    );

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
                LastErrorMessage: entity.LastErrorMessage
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
                LastErrorMessage = dto.LastErrorMessage
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
}
