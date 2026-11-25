namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>Confidence level of a jamming incident, as sent on the wire.</summary>
    public enum JammingConfidenceLevelDto
    {
        /// <summary>Low confidence.</summary>
        Low,
        /// <summary>Medium confidence.</summary>
        Medium,
        /// <summary>High confidence.</summary>
        High,
        /// <summary>Definite confidence.</summary>
        Definite,
    }

    /// <summary>Result of a jamming analysis run for one device, with DTO incidents.</summary>
    public record JammingAnalysisReportDto(
        bool Success,
        string? ErrorMessage,
        Guid DeviceId,
        DateTime AnalysisFromUtc,
        DateTime AnalysisToUtc,
        JammingStatsSummaryDto Summary,
        IReadOnlyList<JammingIncidentDto> Incidents,
        DateTime AnalyzedAtUtc,
        string Message,
        int NewlyDetectedCount);
}
