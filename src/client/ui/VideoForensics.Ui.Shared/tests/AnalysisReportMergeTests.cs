namespace VideoForensics.Ui.Shared.Tests;

using Xunit;
using VideoForensics.Data.Core.Models;
using VideoForensics.Ui.Shared.Services.Analysis;

/// <summary>Per-device reports combine into one report: lists concatenate and the window is kept.</summary>
public class AnalysisReportMergeTests
{
    private static readonly DateTime From = new(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime To = new(2026, 9, 8, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Merge_AccessControlReports_ConcatenatesEventsNewestFirst()
    {
        var older = new AccessControlReport
        {
            ReportFromUtc = From,
            ReportToUtc = To,
            AccessEvents = new List<AccessControlReport.AccessEvent>
            {
                new() { AccessedAtUtc = new DateTime(2026, 9, 2, 0, 0, 0, DateTimeKind.Utc), Actor = "a", Action = "View", EntityType = "Media" },
            },
        };
        var newer = new AccessControlReport
        {
            ReportFromUtc = From,
            ReportToUtc = To,
            AccessEvents = new List<AccessControlReport.AccessEvent>
            {
                new() { AccessedAtUtc = new DateTime(2026, 9, 5, 0, 0, 0, DateTimeKind.Utc), Actor = "b", Action = "View", EntityType = "Media" },
            },
        };

        var merged = AnalysisReportMerge.Merge(new[] { older, newer });

        Assert.Equal(2, merged.AccessEvents.Count);
        Assert.Equal("b", merged.AccessEvents[0].Actor);
        Assert.Equal(From, merged.ReportFromUtc);
        Assert.Equal(To, merged.ReportToUtc);
    }

    [Fact]
    public void Merge_SignalAnomalyReports_KeepsEveryDeviceFindingsAndJammingRows()
    {
        var first = new SignalAnomalyReport
        {
            ReportFromUtc = From,
            ReportToUtc = To,
            AnomaliesByDevice = new List<SignalAnomalyReport.AnomalyFindings>
            {
                new() { DeviceName = "Front" },
            },
            JammingByDevice = new List<SignalAnomalyReport.JammingSummaryEntry>
            {
                new() { DeviceName = "Front", IncidentCount = 2 },
            },
        };
        var second = new SignalAnomalyReport
        {
            ReportFromUtc = From,
            ReportToUtc = To,
            AnomaliesByDevice = new List<SignalAnomalyReport.AnomalyFindings>
            {
                new() { DeviceName = "Back" },
            },
            JammingByDevice = new List<SignalAnomalyReport.JammingSummaryEntry>
            {
                new() { DeviceName = "Back", IncidentCount = 0 },
            },
        };

        var merged = AnalysisReportMerge.Merge(new[] { first, second });

        Assert.Equal(new[] { "Front", "Back" }, merged.AnomaliesByDevice.Select(a => a.DeviceName));
        Assert.Equal(new[] { "Front", "Back" }, merged.JammingByDevice.Select(j => j.DeviceName));
    }

    [Fact]
    public void Merge_ForensicReports_ConcatenatesEvidenceAndClearsSummary()
    {
        var first = new ForensicAnalysisReport { ReportFromUtc = From, ReportToUtc = To, Summary = "first" };
        var second = new ForensicAnalysisReport { ReportFromUtc = From, ReportToUtc = To, Summary = "second" };

        var merged = AnalysisReportMerge.Merge(new[] { first, second });

        Assert.Empty(merged.EvidenceItems);
        Assert.Null(merged.Summary);
    }
}
