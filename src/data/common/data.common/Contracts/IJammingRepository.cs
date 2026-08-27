using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Repository for jamming incident records and their aggregated per-device statistics.</summary>
    public interface IJammingRepository
    {
        /// <summary>Upserts (inserts or updates) a jamming incident record.</summary>
        Task<JammingIncidentRecord> UpsertIncidentAsync(JammingIncidentRecord incident, CancellationToken ct);

        /// <summary>Lists jamming incidents, optionally filtered by device and date range.</summary>
        Task<IReadOnlyList<JammingIncidentRecord>> ListIncidentsAsync(Guid? deviceId, DateTime? fromUtc, DateTime? toUtc, CancellationToken ct);

        /// <summary>Gets the jamming stats summary for a device.</summary>
        Task<JammingStatsSummary?> GetStatsAsync(Guid deviceId, CancellationToken ct);

        /// <summary>Lists jamming stats summaries for all devices.</summary>
        Task<IReadOnlyList<JammingStatsSummary>> ListStatsAsync(CancellationToken ct);

        /// <summary>Recomputes and upserts the JammingStatsSummary row for a device from its current JammingIncidentRecord rows.</summary>
        Task<JammingStatsSummary> RecomputeStatsAsync(Guid deviceId, CancellationToken ct);
    }
}
