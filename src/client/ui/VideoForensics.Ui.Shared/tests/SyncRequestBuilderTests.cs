namespace VideoForensics.Ui.Shared.Tests;

using VideoForensics.Core.Api.Contracts;
using VideoForensics.Ui.Shared.Services;
using Xunit;

public class SyncRequestBuilderTests
{
    private static readonly DateOnly Start = new(2026, 1, 2);
    private static readonly DateOnly End = new(2026, 2, 3);

    [Fact]
    public void Build_NumericRange_UsesDays()
    {
        SyncNowRequest r = SyncRequestBuilder.Build("Events", "60", Start, End);

        Assert.Equal("Events", r.SyncType);
        Assert.Equal("Days", r.RangeKind);
        Assert.Equal(60, r.Days);
    }

    [Fact]
    public void Build_AllHistoryRange_SendsAllRange()
    {
        SyncNowRequest r = SyncRequestBuilder.Build("All", "All", Start, End);

        Assert.Equal("All", r.SyncType);
        Assert.Equal("All", r.RangeKind);
        Assert.Null(r.Days);
    }

    [Fact]
    public void Build_CustomRange_RunsFromStartDayThroughEndDayLocalTime()
    {
        SyncNowRequest r = SyncRequestBuilder.Build("All", "Custom", Start, End);

        Assert.Equal("Custom", r.RangeKind);
        Assert.Equal(DateTime.SpecifyKind(new DateTime(2026, 1, 2), DateTimeKind.Local).ToUniversalTime(), r.StartUtc);
        Assert.Equal(DateTime.SpecifyKind(new DateTime(2026, 2, 4), DateTimeKind.Local).ToUniversalTime(), r.EndUtc);
    }

    [Theory]
    [InlineData("Config")]
    [InlineData("Snapshots")]
    public void Build_TypeThatIgnoresTheWindow_SendsSinceLast(string syncType)
    {
        SyncNowRequest r = SyncRequestBuilder.Build(syncType, "60", Start, End);

        Assert.Equal(syncType, r.SyncType);
        Assert.Equal("SinceLast", r.RangeKind);
        Assert.Null(r.Days);
        Assert.Null(r.StartUtc);
    }

    [Fact]
    public void Build_UnrecognizedRange_FallsBackToSinceLast()
    {
        Assert.Equal("SinceLast", SyncRequestBuilder.Build("Events", "SinceLast", Start, End).RangeKind);
    }

    [Theory]
    [InlineData("All", true)]
    [InlineData("Events", true)]
    [InlineData("Config", false)]
    [InlineData("Snapshots", false)]
    public void UsesRange_ByType(string syncType, bool expected)
    {
        Assert.Equal(expected, SyncRequestBuilder.UsesRange(syncType));
    }

    [Fact]
    public void FormatRecords_AllNull_ReturnsNull()
    {
        Assert.Null(SyncRequestBuilder.FormatRecords("{0}", null, null, null, null, null));
    }

    [Fact]
    public void FormatRecords_SnapshotsOnly_ShowsSnapshotCountNotNull()
    {
        string? result = SyncRequestBuilder.FormatRecords("{0}/{1}/{2}/{3}/{4}", null, null, null, null, null, 17, "{0} snapshots");

        Assert.Equal("17 snapshots", result);
    }

    [Fact]
    public void FormatRecords_CountsAndSnapshots_AppendsSnapshots()
    {
        string? result = SyncRequestBuilder.FormatRecords("{0}/{1}/{2}/{3}/{4}", 3, 1, 42, 6, 0, 5, "{0} snapshots");

        Assert.Equal("3/1/42/6/0, 5 snapshots", result);
    }

    [Fact]
    public void FormatRecords_AllNullWithSnapshotFormat_ReturnsNull()
    {
        Assert.Null(SyncRequestBuilder.FormatRecords("{0}", null, null, null, null, null, null, "{0} snapshots"));
    }

    [Fact]
    public void FormatRecords_Counts_FormatsInOrder()
    {
        Assert.Equal("3/1/42/6/0", SyncRequestBuilder.FormatRecords("{0}/{1}/{2}/{3}/{4}", 3, 1, 42, 6, 0));
    }
}
