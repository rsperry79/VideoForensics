using VideoForensics.Core.Api.Contracts;

namespace VideoForensics.Client.Common.Contracts
{
    /// <summary>Jamming analysis and manual incident recording for the Analyze page, with DTO signatures.</summary>
    public interface IJammingToolsOrchestrator
    {
        /// <summary>Runs jamming analysis for a device over a time window.</summary>
        Task<JammingAnalysisReportDto> AnalyzeJammingAsync(Guid deviceId, DateTime fromUtc, DateTime toUtc, CancellationToken ct = default);

        /// <summary>Records a manually entered jamming incident.</summary>
        Task<(bool Success, string Message, JammingIncidentDto? Record)> RecordJammingIncidentAsync(
            Guid deviceId,
            DateTime startUtc,
            DateTime endUtc,
            int affectedEventCount,
            double averageDegradationDb,
            JammingConfidenceLevelDto confidence,
            string? notes,
            CancellationToken ct = default);
    }
}
