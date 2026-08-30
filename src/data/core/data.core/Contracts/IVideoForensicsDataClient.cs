using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Core.Contracts
{
    /// <summary>Top-level facade for accessing the VideoForensics data layer.</summary>
    public interface IVideoForensicsDataClient
    {
        /// <summary>Registers a new device in the data store.</summary>
        Task<Device> RegisterDeviceAsync(Device device, CancellationToken ct);

        /// <summary>Checks if a media item was already downloaded for a given device and provider event ID.</summary>
        Task<bool> IsMediaAlreadyDownloadedAsync(Guid deviceId, string providerEventId, CancellationToken ct);

        /// <summary>
        /// Records a download event and associated media item(s) atomically via IUnitOfWork,
        /// along with an action log entry.
        /// </summary>
        Task<DownloadEvent> RecordDownloadEventAsync(DownloadEvent evt, MediaItem? media, CancellationToken ct);

        /// <summary>
        /// Upserts an event record (independent of download status) by device ID + provider event ID.
        /// Call once when an event is discovered, and again once it's downloaded/hashed to enrich it.
        /// </summary>
        Task<Event> UpsertEventAsync(Event evt, CancellationToken ct);

        /// <summary>Resolves the effective start date for a download window, considering the watermark.</summary>
        Task<DateTime> GetWatermarkAsync(Guid deviceId, DateTime requestedStartDate, bool force, CancellationToken ct);

        /// <summary>Ensures a user and provider account exist, creating them if necessary.</summary>
        Task<(User User, ProviderAccount Account)> EnsureUserAndAccountAsync(
            string providerName,
            string providerUserKey,
            string displayName,
            string? email,
            CancellationToken ct);

        /// <summary>Ensures a location exists for a provider account, creating it if necessary.</summary>
        Task<Location> EnsureLocationAsync(
            Guid providerAccountId,
            string providerLocationId,
            string name,
            string? address,
            CancellationToken ct);

        /// <summary>Ensures a device exists for a location, creating or updating it as necessary.</summary>
        Task<Device> EnsureDeviceAsync(
            Guid locationId,
            string providerDeviceId,
            string name,
            string type,
            bool isOnline,
            CancellationToken ct);

        /// <summary>Updates the device watermark to mark a successful download completion point for resumable batch downloads.</summary>
        Task UpdateDeviceWatermarkAsync(Guid deviceId, DateTime latestSuccessfulPullTime, CancellationToken ct);

        /// <summary>Records a point-in-time device health/connectivity telemetry snapshot.</summary>
        Task<DeviceHealthSnapshot> RecordDeviceHealthSnapshotAsync(DeviceHealthSnapshot snapshot, CancellationToken ct);

        /// <summary>Gets the credential repository for direct credential access.</summary>
        ICredentialRepository Credentials { get; }

        /// <summary>Gets the integrity verification service for file hash operations.</summary>
        IIntegrityVerificationService IntegrityVerification { get; }

        /// <summary>Gets the action log repository for audit trail access.</summary>
        IActionLogRepository ActionLog { get; }
    }
}
