using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Core.Contracts
{
    /// <summary>Service for recording evidence export operations.</summary>
    public interface IExportRecordService
    {
        /// <summary>
        /// Records an export operation, capturing metadata, archive hash, and per-item hashes at export time.
        /// Logs a single ActionLog entry with action "EvidenceExported".
        /// </summary>
        Task<ExportRecord> RecordExportAsync(
            string exportedByUserName,
            string? caseReference,
            string? recipientDescription,
            string archiveFileName,
            string archiveSha256Hash,
            bool wasEncrypted,
            IReadOnlyList<(Guid MediaItemId, string HashAtExport)> items,
            CancellationToken ct);

        /// <summary>Gets the export history for a media item.</summary>
        Task<IReadOnlyList<ExportRecord>> GetHistoryForMediaItemAsync(Guid mediaItemId, CancellationToken ct);

        /// <summary>Gets the export history for a device.</summary>
        Task<IReadOnlyList<ExportRecord>> GetHistoryForDeviceAsync(Guid deviceId, CancellationToken ct);
    }
}
