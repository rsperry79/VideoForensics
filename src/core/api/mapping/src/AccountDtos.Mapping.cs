using VideoForensics.Core.Api.Contracts;

namespace VideoForensics.Core.Api.Contracts
{
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
                SnapshotRssiIntervalSeconds: entity.SnapshotRssiIntervalSeconds,
                IsEnabled: entity.IsEnabled,
                UseAdvancedJammingSchedule: entity.UseAdvancedJammingSchedule,
                EventNextRunUtc: entity.EventNextRunUtc,
                EventLastRunUtc: entity.EventLastRunUtc
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
                SnapshotRssiIntervalSeconds = dto.SnapshotRssiIntervalSeconds,
                IsEnabled = dto.IsEnabled,
                UseAdvancedJammingSchedule = dto.UseAdvancedJammingSchedule,
                EventNextRunUtc = dto.EventNextRunUtc,
                EventLastRunUtc = dto.EventLastRunUtc
            };
        }
    }
}
