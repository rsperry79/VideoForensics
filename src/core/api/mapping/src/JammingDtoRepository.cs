using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Core.Api.Contracts.Repositories
{
    /// <summary>Server-side <see cref="IJammingRepository"/> over <see cref="IJammingWriteRepository"/>, mapping to DTOs.</summary>
    public sealed class JammingDtoRepository(IJammingWriteRepository jamming) : IJammingRepository
    {
        /// <inheritdoc />
        public async Task<JammingIncidentDto> UpsertIncidentAsync(JammingIncidentDto incident, CancellationToken ct) =>
            (await jamming.UpsertIncidentAsync(incident.ToDomain(), ct)).ToDto();

        /// <inheritdoc />
        public async Task<IReadOnlyList<JammingIncidentDto>> ListIncidentsAsync(Guid? deviceId, DateTime? fromUtc, DateTime? toUtc, CancellationToken ct) =>
            (await jamming.ListIncidentsAsync(deviceId, fromUtc, toUtc, ct)).Select(i => i.ToDto()).ToList();

        /// <inheritdoc />
        public async Task<JammingIncidentDto?> GetIncidentAsync(Guid incidentId, CancellationToken ct) =>
            (await jamming.GetIncidentAsync(incidentId, ct))?.ToDto();

        /// <inheritdoc />
        public async Task<JammingStatsSummaryDto?> GetStatsAsync(Guid deviceId, CancellationToken ct) =>
            (await jamming.GetStatsAsync(deviceId, ct))?.ToDto();

        /// <inheritdoc />
        public async Task<IReadOnlyList<JammingStatsSummaryDto>> ListStatsAsync(CancellationToken ct) =>
            (await jamming.ListStatsAsync(ct)).Select(s => s.ToDto()).ToList();

        /// <inheritdoc />
        public async Task<JammingStatsSummaryDto> RecomputeStatsAsync(Guid deviceId, CancellationToken ct) =>
            (await jamming.RecomputeStatsAsync(deviceId, ct)).ToDto();
    }
}
