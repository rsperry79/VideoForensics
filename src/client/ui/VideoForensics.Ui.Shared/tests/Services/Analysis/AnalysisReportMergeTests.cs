using VideoForensics.Client.Ui.Shared.Services.Analysis;
using VideoForensics.Core.Api.Contracts;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Services.Analysis;
/// <summary>Per-device reports combine into one report: lists concatenate and the window is kept.</summary>
public class AnalysisReportMergeTests
{
    private static readonly DateTime From = new(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime To = new(2026, 9, 8, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Merge_AccessControlReports_ConcatenatesEventsNewestFirst()
    {
        var older = new AccessControlReportDto(
            default,
            From,
            To,
            [new AccessEventDto(new DateTime(2026, 9, 2, 0, 0, 0, DateTimeKind.Utc), "a", "View", "Media", null, null)],
            []);
        var newer = new AccessControlReportDto(
            default,
            From,
            To,
            [new AccessEventDto(new DateTime(2026, 9, 5, 0, 0, 0, DateTimeKind.Utc), "b", "View", "Media", null, null)],
            []);

        AccessControlReportDto merged = AnalysisReportMerge.Merge(new[] { older, newer });

        Assert.Equal(2, merged.AccessEvents.Count);
        Assert.Equal("b", merged.AccessEvents[0].Actor);
        Assert.Equal(From, merged.ReportFromUtc);
        Assert.Equal(To, merged.ReportToUtc);
    }

    [Fact]
    public void Merge_SignalAnomalyReports_KeepsEveryDeviceFindingsAndJammingRows()
    {
        var first = new SignalAnomalyReportDto(
            default,
            From,
            To,
            [new AnomalyFindingsDto(Guid.Empty, "Front", [])],
            [new JammingSummaryEntryDto(Guid.Empty, "Front", 2, 0, 0, 0, null, null)]);
        var second = new SignalAnomalyReportDto(
            default,
            From,
            To,
            [new AnomalyFindingsDto(Guid.Empty, "Back", [])],
            [new JammingSummaryEntryDto(Guid.Empty, "Back", 0, 0, 0, 0, null, null)]);

        SignalAnomalyReportDto merged = AnalysisReportMerge.Merge(new[] { first, second });

        Assert.Equal(new[] { "Front", "Back" }, merged.AnomaliesByDevice.Select(a => a.DeviceName));
        Assert.Equal(new[] { "Front", "Back" }, merged.JammingByDevice.Select(j => j.DeviceName));
    }

    [Fact]
    public void Merge_ForensicReports_ConcatenatesEvidenceAndClearsSummary()
    {
        var first = new ForensicAnalysisReportDto(default, From, To, [], [], [], "first");
        var second = new ForensicAnalysisReportDto(default, From, To, [], [], [], "second");

        ForensicAnalysisReportDto merged = AnalysisReportMerge.Merge(new[] { first, second });

        Assert.Empty(merged.EvidenceItems);
        Assert.Null(merged.Summary);
    }
}
