using VideoForensics.Core.Api.Contracts;

namespace VideoForensics.Client.Core.Tools
{
    /// <summary>Converts a jamming analysis report to its DTO form for the Analyze page.</summary>
    public static class JammingAnalysisReportMapping
    {
        /// <summary>Converts a JammingAnalysisReport, including its incidents and summary, to a JammingAnalysisReportDto.</summary>
        public static JammingAnalysisReportDto ToDto(this JammingAnalysisReport report) => new(
            report.Success,
            report.ErrorMessage,
            report.DeviceId,
            report.AnalysisFromUtc,
            report.AnalysisToUtc,
            report.Summary.ToDto(),
            report.Incidents.Select(incident => incident.ToDto()).ToList(),
            report.AnalyzedAtUtc,
            report.Message,
            report.NewlyDetectedCount);
    }
}
