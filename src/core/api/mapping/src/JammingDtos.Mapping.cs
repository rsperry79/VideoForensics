using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Core.Api.Contracts
{
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
