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

        /// <summary>Gets the annotation repository.</summary>
        IAnnotationRepository Annotations { get; }

        /// <summary>Gets the provider reconciliation repository.</summary>
        IProviderReconciliationRepository ProviderReconciliation { get; }

        /// <summary>Gets the export record repository.</summary>
        IExportRecordRepository ExportRecords { get; }
    }
}
