using System.Linq;
using VideoForensics.Core.Api.Contracts;

namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>Extension methods for mapping backup result types to/from DTOs.</summary>
    public static class BackupDtoMapping
    {
        /// <summary>
        /// Converts a BackupImportStageResult to a BackupImportStageResultDto.
        /// </summary>
        public static BackupImportStageResultDto ToDto(
            this VideoForensics.Client.Common.Contracts.BackupImportStageResult entity)
        {
            return new BackupImportStageResultDto(
                Inserted: entity.Inserted,
                SkippedExisting: entity.SkippedExisting,
                SkippedOrphaned: entity.SkippedOrphaned,
                IntegrityIssues: entity.IntegrityIssues
            );
        }

        /// <summary>
        /// Converts a BackupImportStageResultDto to a BackupImportStageResult.
        /// </summary>
        public static VideoForensics.Client.Common.Contracts.BackupImportStageResult ToDomain(
            this BackupImportStageResultDto dto)
        {
            return new VideoForensics.Client.Common.Contracts.BackupImportStageResult
            {
                Inserted = dto.Inserted,
                SkippedExisting = dto.SkippedExisting,
                SkippedOrphaned = dto.SkippedOrphaned,
                IntegrityIssues = dto.IntegrityIssues
            };
        }

        /// <summary>
        /// Converts a PrepareExportResult to a PrepareExportResultDto.
        /// </summary>
        public static PrepareExportResultDto ToDto(
            this VideoForensics.Client.Common.Contracts.PrepareExportResult entity)
        {
            return new PrepareExportResultDto(
                Validated: entity.Validated,
                Backfilled: entity.Backfilled,
                MissingMedia: entity.MissingMedia,
                MissingSidecarUnrecoverable: entity.MissingSidecarUnrecoverable,
                Details: entity.Details.AsReadOnly()
            );
        }

        /// <summary>
        /// Converts a PrepareExportResultDto to a PrepareExportResult.
        /// </summary>
        public static VideoForensics.Client.Common.Contracts.PrepareExportResult ToDomain(
            this PrepareExportResultDto dto)
        {
            return new VideoForensics.Client.Common.Contracts.PrepareExportResult
            {
                Validated = dto.Validated,
                Backfilled = dto.Backfilled,
                MissingMedia = dto.MissingMedia,
                MissingSidecarUnrecoverable = dto.MissingSidecarUnrecoverable,
                Details = dto.Details.ToList()
            };
        }

        /// <summary>
        /// Converts a BackupExportResult to a BackupExportResultDto.
        /// </summary>
        public static BackupExportResultDto ToDto(
            this VideoForensics.Client.Common.Contracts.BackupExportResult entity)
        {
            return new BackupExportResultDto(
                Success: entity.Success,
                ArchivePath: entity.ArchivePath,
                ArchiveSha256Hash: entity.ArchiveSha256Hash,
                ProviderAccountCount: entity.ProviderAccountCount,
                LocationCount: entity.LocationCount,
                DeviceCount: entity.DeviceCount,
                EventCount: entity.EventCount,
                DownloadEventCount: entity.DownloadEventCount,
                MediaItemCount: entity.MediaItemCount,
                ErrorMessage: entity.ErrorMessage
            );
        }

        /// <summary>
        /// Converts a BackupExportResultDto to a BackupExportResult.
        /// </summary>
        public static VideoForensics.Client.Common.Contracts.BackupExportResult ToDomain(
            this BackupExportResultDto dto)
        {
            return new VideoForensics.Client.Common.Contracts.BackupExportResult
            {
                Success = dto.Success,
                ArchivePath = dto.ArchivePath,
                ArchiveSha256Hash = dto.ArchiveSha256Hash,
                ProviderAccountCount = dto.ProviderAccountCount,
                LocationCount = dto.LocationCount,
                DeviceCount = dto.DeviceCount,
                EventCount = dto.EventCount,
                DownloadEventCount = dto.DownloadEventCount,
                MediaItemCount = dto.MediaItemCount,
                ErrorMessage = dto.ErrorMessage
            };
        }

        /// <summary>
        /// Converts a BackupImportResult to a BackupImportResultDto.
        /// </summary>
        public static BackupImportResultDto ToDto(
            this VideoForensics.Client.Common.Contracts.BackupImportResult entity)
        {
            return new BackupImportResultDto(
                Success: entity.Success,
                ProviderAccounts: entity.ProviderAccounts.ToDto(),
                Locations: entity.Locations.ToDto(),
                Devices: entity.Devices.ToDto(),
                Events: entity.Events.ToDto(),
                DownloadEvents: entity.DownloadEvents.ToDto(),
                MediaItems: entity.MediaItems.ToDto(),
                Details: entity.Details.AsReadOnly(),
                ErrorMessage: entity.ErrorMessage
            );
        }

        /// <summary>
        /// Converts a BackupImportResultDto to a BackupImportResult.
        /// </summary>
        public static VideoForensics.Client.Common.Contracts.BackupImportResult ToDomain(
            this BackupImportResultDto dto)
        {
            return new VideoForensics.Client.Common.Contracts.BackupImportResult
            {
                Success = dto.Success,
                ProviderAccounts = dto.ProviderAccounts.ToDomain(),
                Locations = dto.Locations.ToDomain(),
                Devices = dto.Devices.ToDomain(),
                Events = dto.Events.ToDomain(),
                DownloadEvents = dto.DownloadEvents.ToDomain(),
                MediaItems = dto.MediaItems.ToDomain(),
                Details = dto.Details.ToList(),
                ErrorMessage = dto.ErrorMessage
            };
        }
    }
}
