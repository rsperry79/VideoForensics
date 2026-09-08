using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Repository for export audit trail tracking. Records all export operations with format, count, and purpose tracking.</summary>
    public interface IExportAuditRecordRepository
    {
        /// <summary>Records an evidence export operation in the audit trail.</summary>
        /// <param name="locationId">The ID of the location from which evidence was exported.</param>
        /// <param name="exportedBy">The user who performed the export.</param>
        /// <param name="eventsExported">The number of events that were exported.</param>
        /// <param name="exportFormat">The format used for export: "AES256Zip", "PDF", etc.</param>
        /// <param name="purpose">The stated purpose for exporting (compliance requirement).</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The created export audit record.</returns>
        Task<ExportAuditRecordEntity> RecordExportAsync(
            Guid locationId,
            string exportedBy,
            int eventsExported,
            string exportFormat,
            string purpose,
            CancellationToken ct);

        /// <summary>Gets a single export audit record by ID.</summary>
        Task<ExportAuditRecordEntity?> GetAsync(Guid recordId, CancellationToken ct);

        /// <summary>Gets all export records for a specific location (location export history).</summary>
        Task<IReadOnlyList<ExportAuditRecordEntity>> GetForLocationAsync(Guid locationId, CancellationToken ct);

        /// <summary>Gets all export records by a specific user (user export history).</summary>
        Task<IReadOnlyList<ExportAuditRecordEntity>> GetByUserAsync(string userId, CancellationToken ct);

        /// <summary>Gets export records within a date range (time-based audit queries).</summary>
        Task<IReadOnlyList<ExportAuditRecordEntity>> GetByDateRangeAsync(
            DateTime fromUtc,
            DateTime toUtc,
            CancellationToken ct);

        /// <summary>Gets all export records, optionally paginated (full export audit trail).</summary>
        Task<IReadOnlyList<ExportAuditRecordEntity>> ListAsync(
            int skip = 0,
            int take = 1000,
            CancellationToken ct = default);

        /// <summary>Gets statistics about exports: total count, events exported, formats used, etc.</summary>
        /// <returns>A summary object with export statistics.</returns>
        Task<ExportStatistics> GetStatisticsAsync(CancellationToken ct);
    }

    /// <summary>Summary statistics for export operations (used for compliance reporting).</summary>
    public class ExportStatistics
    {
        public int TotalExports { get; set; }
        public int TotalEventsExported { get; set; }
        public Dictionary<string, int> ExportsByFormat { get; set; } = new();
        public DateTime? FirstExportAtUtc { get; set; }
        public DateTime? LastExportAtUtc { get; set; }
        public int UniqueExporters { get; set; }
    }
}
