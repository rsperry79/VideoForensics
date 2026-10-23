using VideoForensics.Client.WebApp.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Client.Ui.Shared.Layout;

namespace VideoForensics.Client.WebApp.Services.CircuitGuards
{
    /// <summary>
    /// Guards recording a jamming incident and recomputing its stats, which the Jamming tab makes in-process. Matches
    /// PUT /api/v1/jamming/incidents and POST /api/v1/jamming/stats/{id}/recompute, both Admin. Reads stay open.
    /// Recording an incident can also create a case; that happens inside this Admin-gated call.
    /// </summary>
    public sealed class GuardedJammingRepository(IJammingRepository inner, ICircuitOperatorGate gate) : IJammingRepository
    {
        public Task<JammingIncidentRecord> UpsertIncidentAsync(JammingIncidentRecord incident, CancellationToken ct)
        {
            Require();
            return inner.UpsertIncidentAsync(incident, ct);
        }

        public Task<IReadOnlyList<JammingIncidentRecord>> ListIncidentsAsync(Guid? deviceId, DateTime? fromUtc, DateTime? toUtc, CancellationToken ct)
        {
            return inner.ListIncidentsAsync(deviceId, fromUtc, toUtc, ct);
        }

        public Task<JammingIncidentRecord?> GetIncidentAsync(Guid incidentId, CancellationToken ct)
        {
            return inner.GetIncidentAsync(incidentId, ct);
        }

        public Task<JammingStatsSummary?> GetStatsAsync(Guid deviceId, CancellationToken ct)
        {
            return inner.GetStatsAsync(deviceId, ct);
        }

        public Task<IReadOnlyList<JammingStatsSummary>> ListStatsAsync(CancellationToken ct)
        {
            return inner.ListStatsAsync(ct);
        }

        public Task<JammingStatsSummary> RecomputeStatsAsync(Guid deviceId, CancellationToken ct)
        {
            Require();
            return inner.RecomputeStatsAsync(deviceId, ct);
        }

        private void Require()
        {
            if (!gate.Allows(AccessPolicy.Admin))
            {
                throw new UnauthorizedAccessException(gate.Denial(AccessPolicy.Admin));
            }
        }
    }
}
