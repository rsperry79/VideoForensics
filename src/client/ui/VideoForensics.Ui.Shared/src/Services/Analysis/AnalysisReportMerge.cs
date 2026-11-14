using VideoForensics.Core.Api.Contracts;

namespace VideoForensics.Client.Ui.Shared.Services.Analysis;

/// <summary>
/// Combines per-device analysis reports into one report. The server builds each report for a single device (or
/// all devices), so a multi-device view requests each device and merges the lists here. Lists are concatenated in
/// the order given; a combined report carries no summary text of its own.
/// </summary>
public static class AnalysisReportMerge
{
    public static ForensicAnalysisReportDto Merge(IReadOnlyList<ForensicAnalysisReportDto> reports)
    {
        ForensicAnalysisReportDto first = reports[0];
        return first with
        {
            GeneratedAtUtc = reports.Max(r => r.GeneratedAtUtc),
            ReportFromUtc = first.ReportFromUtc,
            ReportToUtc = first.ReportToUtc,
            EvidenceItems = reports.SelectMany(r => r.EvidenceItems).ToList(),
            AnomalousHealthSnapshots = reports.SelectMany(r => r.AnomalousHealthSnapshots).ToList(),
            SignificantActions = reports.SelectMany(r => r.SignificantActions).OrderByDescending(a => a.TimestampUtc).ToList(),
            Summary = null,
        };
    }

    public static SignalAnomalyReportDto Merge(IReadOnlyList<SignalAnomalyReportDto> reports)
    {
        SignalAnomalyReportDto first = reports[0];
        return first with
        {
            GeneratedAtUtc = reports.Max(r => r.GeneratedAtUtc),
            ReportFromUtc = first.ReportFromUtc,
            ReportToUtc = first.ReportToUtc,
            AnomaliesByDevice = reports.SelectMany(r => r.AnomaliesByDevice).ToList(),
            JammingByDevice = reports.SelectMany(r => r.JammingByDevice).ToList(),
        };
    }

    public static AccessControlReportDto Merge(IReadOnlyList<AccessControlReportDto> reports)
    {
        AccessControlReportDto first = reports[0];
        return first with
        {
            GeneratedAtUtc = reports.Max(r => r.GeneratedAtUtc),
            ReportFromUtc = first.ReportFromUtc,
            ReportToUtc = first.ReportToUtc,
            AccessEvents = reports.SelectMany(r => r.AccessEvents).OrderByDescending(e => e.AccessedAtUtc).ToList(),
            ExportEvents = reports.SelectMany(r => r.ExportEvents).OrderByDescending(e => e.ExportedAtUtc).ToList(),
        };
    }
}
