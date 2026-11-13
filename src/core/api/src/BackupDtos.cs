namespace VideoForensics.Core.Api.Contracts
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
}
