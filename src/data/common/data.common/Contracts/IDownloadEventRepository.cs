using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Repository for download event entities.</summary>
    public interface IDownloadEventRepository
    {
        /// <summary>Gets a download event by ID.</summary>
        Task<DownloadEvent?> GetAsync(Guid eventId, CancellationToken ct);

        /// <summary>Gets download events by device ID.</summary>
        Task<IReadOnlyList<DownloadEvent>> GetByDeviceIdAsync(Guid deviceId, CancellationToken ct);

        /// <summary>Gets the latest successful download event time for a device (watermark).</summary>
        Task<DateTime?> GetLatestSuccessfulEventTimeAsync(Guid deviceId, CancellationToken ct);

        /// <summary>Checks if a download event exists for a given provider event ID on a device.</summary>
        Task<bool> ExistsForProviderEventIdAsync(Guid deviceId, string providerEventId, CancellationToken ct);

        /// <summary>Gets a download event by device ID and provider event ID.</summary>
        Task<DownloadEvent?> GetByProviderEventIdAsync(Guid deviceId, string providerEventId, CancellationToken ct);

        /// <summary>Lists all download events.</summary>
        Task<IReadOnlyList<DownloadEvent>> ListAsync(CancellationToken ct);

        /// <summary>Adds a new download event.</summary>
        Task AddAsync(DownloadEvent downloadEvent, CancellationToken ct);

        /// <summary>Updates an existing download event.</summary>
        Task UpdateAsync(DownloadEvent downloadEvent, CancellationToken ct);

        /// <summary>Deletes a download event.</summary>
        Task DeleteAsync(Guid eventId, CancellationToken ct);
    }
}
