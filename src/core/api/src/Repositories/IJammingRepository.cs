namespace VideoForensics.Core.Api.Contracts.Repositories
{
    /// <summary>Jamming incidents and statistics, with DTO signatures. Client hosts use this interface.</summary>
    public interface IJammingRepository
    {
        /// <summary>Creates or updates a jamming incident and returns it.</summary>
        Task<JammingIncidentDto> UpsertIncidentAsync(JammingIncidentDto incident, CancellationToken ct);

        /// <summary>Lists incidents, optionally filtered by device and UTC range.</summary>
        Task<IReadOnlyList<JammingIncidentDto>> ListIncidentsAsync(Guid? deviceId, DateTime? fromUtc, DateTime? toUtc, CancellationToken ct);

        /// <summary>Gets an incident by id, or null.</summary>
        Task<JammingIncidentDto?> GetIncidentAsync(Guid incidentId, CancellationToken ct);

        /// <summary>Jamming statistics for a device, or null.</summary>
        Task<JammingStatsSummaryDto?> GetStatsAsync(Guid deviceId, CancellationToken ct);

        /// <summary>Jamming statistics for every device.</summary>
        Task<IReadOnlyList<JammingStatsSummaryDto>> ListStatsAsync(CancellationToken ct);

        /// <summary>Recomputes and returns a device's jamming statistics.</summary>
        Task<JammingStatsSummaryDto> RecomputeStatsAsync(Guid deviceId, CancellationToken ct);
    }
}
