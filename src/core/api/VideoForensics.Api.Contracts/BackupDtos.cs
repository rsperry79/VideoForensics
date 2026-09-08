namespace VideoForensics.Api.Contracts
{
    /// <summary>
    /// Per-entity-type counts from a backup import stage.
    /// </summary>
    /// <param name="Inserted">Number of records inserted.</param>
    /// <param name="SkippedExisting">Number of records skipped because they already exist.</param>
    /// <param name="SkippedOrphaned">Number of records skipped due to missing parent records.</param>
    /// <param name="IntegrityIssues">Number of records rejected due to integrity verification failures.</param>
    public record BackupImportStageResultDto(
        int Inserted,
        int SkippedExisting,
        int SkippedOrphaned,
        int IntegrityIssues
    );

    /// <summary>
    /// Result of validating that every downloaded event's media/sidecar is present and correctly tagged, ahead of a backup export.
    /// </summary>
    /// <param name="Validated">Number of events successfully validated.</param>
    /// <param name="Backfilled">Number of missing sidecars/tags backfilled.</param>
    /// <param name="MissingMedia">Number of events with missing media files that could not be recovered.</param>
    /// <param name="MissingSidecarUnrecoverable">Number of events with unrecoverable missing sidecar/media-tag entries.</param>
    /// <param name="Details">Detailed messages from the validation run.</param>
    public record PrepareExportResultDto(
        int Validated,
        int Backfilled,
        int MissingMedia,
        int MissingSidecarUnrecoverable,
        IReadOnlyList<string> Details
    );

    /// <summary>
    /// Result of exporting the database to a JSON+zip backup archive.
    /// </summary>
    /// <param name="Success">True if the export completed successfully.</param>
    /// <param name="ArchivePath">Full path to the created backup archive file, if successful.</param>
    /// <param name="ArchiveSha256Hash">SHA-256 hash of the archive file for integrity verification, if successful.</param>
    /// <param name="ProviderAccountCount">Number of provider accounts included in the backup.</param>
    /// <param name="LocationCount">Number of locations included in the backup.</param>
    /// <param name="DeviceCount">Number of devices included in the backup.</param>
    /// <param name="EventCount">Number of events included in the backup.</param>
    /// <param name="DownloadEventCount">Number of download events included in the backup.</param>
    /// <param name="MediaItemCount">Number of media items included in the backup.</param>
    /// <param name="ErrorMessage">Error message if the export failed, null if successful.</param>
    public record BackupExportResultDto(
        bool Success,
        string? ArchivePath,
        string? ArchiveSha256Hash,
        int ProviderAccountCount,
        int LocationCount,
        int DeviceCount,
        int EventCount,
        int DownloadEventCount,
        int MediaItemCount,
        string? ErrorMessage
    );

    /// <summary>
    /// Result of importing a backup archive back into the database.
    /// </summary>
    /// <param name="Success">True if the import completed successfully (or partially with issues logged).</param>
    /// <param name="ProviderAccounts">Import results for provider accounts.</param>
    /// <param name="Locations">Import results for locations.</param>
    /// <param name="Devices">Import results for devices.</param>
    /// <param name="Events">Import results for events.</param>
    /// <param name="DownloadEvents">Import results for download events.</param>
    /// <param name="MediaItems">Import results for media items.</param>
    /// <param name="Details">Detailed messages from the import run.</param>
    /// <param name="ErrorMessage">Error message if the import failed entirely, null if successful.</param>
    public record BackupImportResultDto(
        bool Success,
        BackupImportStageResultDto ProviderAccounts,
        BackupImportStageResultDto Locations,
        BackupImportStageResultDto Devices,
        BackupImportStageResultDto Events,
        BackupImportStageResultDto DownloadEvents,
        BackupImportStageResultDto MediaItems,
        IReadOnlyList<string> Details,
        string? ErrorMessage
    );

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
