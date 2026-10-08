using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Api.Contracts
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

    /// <summary>Extension methods for mapping jamming entities to/from their wire DTOs.</summary>
    public static class JammingDtoMapping
    {
        /// <summary>Converts a <see cref="JammingIncidentRecord"/> to a <see cref="JammingIncidentDto"/>.</summary>
        public static JammingIncidentDto ToDto(this JammingIncidentRecord entity)
        {
            return new JammingIncidentDto(
                Id: entity.Id,
                DeviceId: entity.DeviceId,
                StartUtc: entity.StartUtc,
                EndUtc: entity.EndUtc,
                AffectedEventCount: entity.AffectedEventCount,
                AverageDegradationDb: entity.AverageDegradationDb,
                Confidence: entity.Confidence.ToString(),
                DetectedAtUtc: entity.DetectedAtUtc,
                Notes: entity.Notes,
                Source: entity.Source.ToString(),
                CaseId: entity.CaseId
            );
        }

        /// <summary>Converts a <see cref="JammingIncidentDto"/> to a <see cref="JammingIncidentRecord"/>.</summary>
        public static JammingIncidentRecord ToDomain(this JammingIncidentDto dto)
        {
            return new JammingIncidentRecord
            {
                Id = dto.Id,
                DeviceId = dto.DeviceId,
                StartUtc = dto.StartUtc,
                EndUtc = dto.EndUtc,
                AffectedEventCount = dto.AffectedEventCount,
                AverageDegradationDb = dto.AverageDegradationDb,
                Confidence = Enum.Parse<JammingConfidenceLevel>(dto.Confidence, ignoreCase: true),
                DetectedAtUtc = dto.DetectedAtUtc,
                Notes = dto.Notes,
                Source = Enum.Parse<JammingIncidentSource>(dto.Source, ignoreCase: true),
                CaseId = dto.CaseId
            };
        }

        /// <summary>
        /// Converts an <see cref="UpsertJammingIncidentRequest"/> to a <see cref="JammingIncidentRecord"/>.
        /// <c>Source</c> is parsed from the request (null = ManuallyRecorded; the server validates the name first);
        /// <c>DetectedAtUtc</c> = default and <c>CaseId</c> = null are server-controlled and must be overlaid by the caller.
        /// </summary>
        public static JammingIncidentRecord ToDomain(this UpsertJammingIncidentRequest request)
        {
            return new JammingIncidentRecord
            {
                Id = request.Id,
                DeviceId = request.DeviceId,
                StartUtc = request.StartUtc,
                EndUtc = request.EndUtc,
                AffectedEventCount = request.AffectedEventCount,
                AverageDegradationDb = request.AverageDegradationDb,
                Confidence = Enum.Parse<JammingConfidenceLevel>(request.Confidence, ignoreCase: true),
                Notes = request.Notes,
                Source = request.Source == null
                    ? JammingIncidentSource.ManuallyRecorded
                    : Enum.Parse<JammingIncidentSource>(request.Source, ignoreCase: true),
                CaseId = null
            };
        }

        /// <summary>Converts a <see cref="JammingStatsSummary"/> to a <see cref="JammingStatsSummaryDto"/>.</summary>
        public static JammingStatsSummaryDto ToDto(this JammingStatsSummary entity)
        {
            return new JammingStatsSummaryDto(
                Id: entity.Id,
                DeviceId: entity.DeviceId,
                IncidentCount: entity.IncidentCount,
                TotalJammedDurationMinutes: entity.TotalJammedDurationMinutes,
                AverageDegradationDb: entity.AverageDegradationDb,
                MaxDegradationDb: entity.MaxDegradationDb,
                LowConfidenceCount: entity.LowConfidenceCount,
                MediumConfidenceCount: entity.MediumConfidenceCount,
                HighConfidenceCount: entity.HighConfidenceCount,
                DefiniteConfidenceCount: entity.DefiniteConfidenceCount,
                FirstIncidentUtc: entity.FirstIncidentUtc,
                LastIncidentUtc: entity.LastIncidentUtc,
                LastUpdatedUtc: entity.LastUpdatedUtc
            );
        }

        /// <summary>Converts a <see cref="JammingStatsSummaryDto"/> to a <see cref="JammingStatsSummary"/>.</summary>
        public static JammingStatsSummary ToDomain(this JammingStatsSummaryDto dto)
        {
            return new JammingStatsSummary
            {
                Id = dto.Id,
                DeviceId = dto.DeviceId,
                IncidentCount = dto.IncidentCount,
                TotalJammedDurationMinutes = dto.TotalJammedDurationMinutes,
                AverageDegradationDb = dto.AverageDegradationDb,
                MaxDegradationDb = dto.MaxDegradationDb,
                LowConfidenceCount = dto.LowConfidenceCount,
                MediumConfidenceCount = dto.MediumConfidenceCount,
                HighConfidenceCount = dto.HighConfidenceCount,
                DefiniteConfidenceCount = dto.DefiniteConfidenceCount,
                FirstIncidentUtc = dto.FirstIncidentUtc,
                LastIncidentUtc = dto.LastIncidentUtc,
                LastUpdatedUtc = dto.LastUpdatedUtc
            };
        }
    }
}