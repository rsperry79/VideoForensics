namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>
    /// Wire DTO for a jamming or signal-interference incident recorded against a device.
    /// </summary>
    /// <param name="Id">Unique identifier of the incident.</param>
    /// <param name="DeviceId">The device the incident was observed on.</param>
    /// <param name="StartUtc">When the interference began, in UTC.</param>
    /// <param name="EndUtc">When the interference ended, in UTC.</param>
    /// <param name="AffectedEventCount">Number of events affected during the incident.</param>
    /// <param name="AverageDegradationDb">Average signal degradation in dB (non-negative).</param>
    /// <param name="Confidence">Confidence level name (Low, Medium, High, Definite).</param>
    /// <param name="DetectedAtUtc">When the incident was recorded or detected, in UTC. Server-controlled.</param>
    /// <param name="Notes">Optional free-text notes (max 2000 characters).</param>
    /// <param name="Source">Origin of the incident (AutoDetected or ManuallyRecorded). Server-controlled.</param>
    /// <param name="CaseId">Forensic case the server auto-created for this incident, if any. Server-controlled.</param>
    public record JammingIncidentDto(
        Guid Id,
        Guid DeviceId,
        DateTime StartUtc,
        DateTime EndUtc,
        int AffectedEventCount,
        double AverageDegradationDb,
        string Confidence,
        DateTime DetectedAtUtc,
        string? Notes,
        string Source,
        Guid? CaseId
    );

    /// <summary>
    /// Request body to record (insert or update) a jamming incident. Deliberately carries no
    /// <c>CaseId</c> or <c>DetectedAtUtc</c>: those are server-controlled so a client cannot forge a
    /// case link or rewrite when an incident was detected. <c>Source</c> is the one client-claimed
    /// provenance field (the write route is Admin-only): it lets a client host that ran the analysis
    /// itself record incidents as AutoDetected so re-runs dedupe; it applies to NEW incidents only.
    /// </summary>
    /// <param name="Id">Incident id; <see cref="Guid.Empty"/> (or an unknown id) records a new incident.</param>
    /// <param name="DeviceId">The device the incident was observed on.</param>
    /// <param name="StartUtc">When the interference began, in UTC. Must be before <paramref name="EndUtc"/>.</param>
    /// <param name="EndUtc">When the interference ended, in UTC.</param>
    /// <param name="AffectedEventCount">Number of events affected (non-negative).</param>
    /// <param name="AverageDegradationDb">Average signal degradation in dB (non-negative).</param>
    /// <param name="Confidence">Confidence level name (Low, Medium, High, Definite).</param>
    /// <param name="Notes">Optional free-text notes (max 2000 characters).</param>
    /// <param name="Source">Optional origin name (AutoDetected or ManuallyRecorded, case-insensitive); null means ManuallyRecorded. Applied to new incidents only; an existing incident keeps its stored source.</param>
    public record UpsertJammingIncidentRequest(
        Guid Id,
        Guid DeviceId,
        DateTime StartUtc,
        DateTime EndUtc,
        int AffectedEventCount,
        double AverageDegradationDb,
        string Confidence,
        string? Notes,
        string? Source = null
    );

    /// <summary>Wire DTO for the aggregated jamming statistics of a single device.</summary>
    /// <param name="Id">Unique identifier of the summary row.</param>
    /// <param name="DeviceId">The device the statistics describe.</param>
    /// <param name="IncidentCount">Total number of incidents.</param>
    /// <param name="TotalJammedDurationMinutes">Sum of all incident durations, in minutes.</param>
    /// <param name="AverageDegradationDb">Mean of the per-incident average degradation, in dB.</param>
    /// <param name="MaxDegradationDb">Largest per-incident average degradation, in dB.</param>
    /// <param name="LowConfidenceCount">Incidents classified Low.</param>
    /// <param name="MediumConfidenceCount">Incidents classified Medium.</param>
    /// <param name="HighConfidenceCount">Incidents classified High.</param>
    /// <param name="DefiniteConfidenceCount">Incidents classified Definite.</param>
    /// <param name="FirstIncidentUtc">Start of the earliest incident, if any.</param>
    /// <param name="LastIncidentUtc">Start of the latest incident, if any.</param>
    /// <param name="LastUpdatedUtc">When the summary was last recomputed, in UTC.</param>
    public record JammingStatsSummaryDto(
        Guid Id,
        Guid DeviceId,
        int IncidentCount,
        double TotalJammedDurationMinutes,
        double AverageDegradationDb,
        double MaxDegradationDb,
        int LowConfidenceCount,
        int MediumConfidenceCount,
        int HighConfidenceCount,
        int DefiniteConfidenceCount,
        DateTime? FirstIncidentUtc,
        DateTime? LastIncidentUtc,
        DateTime LastUpdatedUtc
    );
}
