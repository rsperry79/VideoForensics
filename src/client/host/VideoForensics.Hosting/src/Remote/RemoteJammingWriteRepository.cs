using VideoForensics.Core.Api.Contracts;
using VideoForensics.Core.Api.Contracts.Repositories;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.Hosting.Remote
{
    /// <summary>
    /// Client-side <see cref="IJammingWriteRepository"/> for the entity-typed jamming tools (JammingToolsOrchestrator). It calls the
    /// server through <see cref="IJammingRepository"/> and maps each entity to its DTO and back.
    /// </summary>
    public sealed class RemoteJammingWriteRepository(IJammingRepository remote) : IJammingWriteRepository
    {
        /// <inheritdoc />
        public async Task<JammingIncidentRecord> UpsertIncidentAsync(JammingIncidentRecord incident, CancellationToken ct) =>
            (await remote.UpsertIncidentAsync(incident.ToDto(), ct)).ToDomain();

        /// <inheritdoc />
        public async Task<IReadOnlyList<JammingIncidentRecord>> ListIncidentsAsync(Guid? deviceId, DateTime? fromUtc, DateTime? toUtc, CancellationToken ct) =>
            (await remote.ListIncidentsAsync(deviceId, fromUtc, toUtc, ct)).Select(i => i.ToDomain()).ToList();

        /// <inheritdoc />
        public async Task<JammingIncidentRecord?> GetIncidentAsync(Guid incidentId, CancellationToken ct) =>
            (await remote.GetIncidentAsync(incidentId, ct))?.ToDomain();

        /// <inheritdoc />
        public async Task<JammingStatsSummary?> GetStatsAsync(Guid deviceId, CancellationToken ct) =>
            (await remote.GetStatsAsync(deviceId, ct))?.ToDomain();

        /// <inheritdoc />
        public async Task<IReadOnlyList<JammingStatsSummary>> ListStatsAsync(CancellationToken ct) =>
            (await remote.ListStatsAsync(ct)).Select(s => s.ToDomain()).ToList();

        /// <inheritdoc />
        public async Task<JammingStatsSummary> RecomputeStatsAsync(Guid deviceId, CancellationToken ct) =>
            (await remote.RecomputeStatsAsync(deviceId, ct)).ToDomain();
    }
}
