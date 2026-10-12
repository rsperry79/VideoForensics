using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Context providing repository instances bound to a shared transaction for multi-entity atomicity.</summary>
    public interface IUnitOfWorkContext
    {
        /// <summary>Gets the user repository.</summary>
        IUserRepository Users { get; }

        /// <summary>Gets the provider account repository.</summary>
        IProviderAccountRepository ProviderAccounts { get; }

        /// <summary>Gets the location repository.</summary>
        ILocationRepository Locations { get; }

        /// <summary>Gets the device repository.</summary>
        IDeviceRepository Devices { get; }

        /// <summary>Gets the media item repository.</summary>
        IMediaItemRepository MediaItems { get; }

        /// <summary>Gets the download event repository.</summary>
        IDownloadEventRepository DownloadEvents { get; }

        /// <summary>Gets the credential repository.</summary>
        ICredentialRepository Credentials { get; }

        /// <summary>Gets the action log repository.</summary>
        IActionLogRepository ActionLog { get; }

        /// <summary>Gets the event repository.</summary>
        IEventRepository Events { get; }

        /// <summary>Gets the device config repository.</summary>
        IDeviceConfigRepository DeviceConfig { get; }

        /// <summary>Gets the provider reconciliation repository.</summary>
        IProviderReconciliationRepository ProviderReconciliation { get; }

        /// <summary>Gets the export record repository.</summary>
        IExportRecordRepository ExportRecords { get; }

        /// <summary>Gets the access audit log repository (compliance: evidence access tracking).</summary>
        IAccessAuditLogRepository AccessAuditLogs { get; }

        /// <summary>Gets the export audit record repository (compliance: export operation tracking).</summary>
        IExportAuditRecordRepository ExportAuditRecords { get; }

        /// <summary>Exposes DbSets for detection-related entities to enable direct persistence in transactions.</summary>
        IDetectionEntityProvider DetectionEntities { get; }
    }

    /// <summary>Provides access to detection-related DbSets within a transaction context.</summary>
    public interface IDetectionEntityProvider
    {
        /// <summary>Adds a media item detection to the current transaction context.</summary>
        Task AddMediaItemDetectionAsync(MediaItemDetection detection, CancellationToken ct);

        /// <summary>Adds detected persons to the current transaction context.</summary>
        Task AddDetectedPersonsAsync(List<DetectedPerson> persons, CancellationToken ct);

        /// <summary>Adds detection type occurrences to the current transaction context.</summary>
        Task AddDetectionTypeOccurrencesAsync(List<DetectionTypeOccurrence> occurrences, CancellationToken ct);

        /// <summary>Adds an event detection to the current transaction context.</summary>
        Task AddEventDetectionAsync(EventDetection detection, CancellationToken ct);

        /// <summary>Adds event detection zones to the current transaction context.</summary>
        Task AddEventDetectionZonesAsync(List<EventDetectionZone> zones, CancellationToken ct);

        /// <summary>Adds event security alerts to the current transaction context.</summary>
        Task AddEventSecurityAlertsAsync(List<EventSecurityAlert> alerts, CancellationToken ct);

        /// <summary>Adds event detected persons to the current transaction context.</summary>
        Task AddEventDetectedPersonsAsync(List<EventDetectedPerson> persons, CancellationToken ct);

        /// <summary>Adds event detection type occurrences to the current transaction context.</summary>
        Task AddEventDetectionTypeOccurrencesAsync(List<EventDetectionTypeOccurrence> occurrences, CancellationToken ct);
    }
}
