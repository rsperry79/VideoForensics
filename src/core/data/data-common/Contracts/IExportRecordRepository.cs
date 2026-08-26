using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Repository for export record entities.</summary>
    public interface IExportRecordRepository
    {
        /// <summary>Gets an export record by ID.</summary>
        Task<ExportRecord?> GetAsync(Guid recordId, CancellationToken ct);

        /// <summary>Gets export record items for an export record.</summary>
        Task<IReadOnlyList<ExportRecordItem>> GetItemsForRecordAsync(Guid exportRecordId, CancellationToken ct);

        /// <summary>Appends a new export record with its items in a single atomic operation.</summary>
        Task<ExportRecord> AppendAsync(ExportRecord record, IReadOnlyList<ExportRecordItem> items, CancellationToken ct);

        /// <summary>Gets export history for a specific media item.</summary>
        Task<IReadOnlyList<ExportRecord>> GetHistoryForMediaItemAsync(Guid mediaItemId, CancellationToken ct);

        /// <summary>Gets export history for a specific device.</summary>
        Task<IReadOnlyList<ExportRecord>> GetHistoryForDeviceAsync(Guid deviceId, CancellationToken ct);

        /// <summary>Lists all export records.</summary>
        Task<IReadOnlyList<ExportRecord>> ListAsync(CancellationToken ct);
    }
}
