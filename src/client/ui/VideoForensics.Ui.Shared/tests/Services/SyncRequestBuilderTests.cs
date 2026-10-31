
using System.Text.Json;

using VideoForensics.Client.Ui.Shared.Services;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Services;

public class SyncRequestBuilderTests
{
    private static JsonElement Json(object o)
    {
        return JsonSerializer.SerializeToElement(o, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    [Fact]
    public void Build_NumericRange_UsesDaysKind()
    {
        JsonElement r = Json(SyncRequestBuilder.Build("Events", "60", null, null));
        Assert.Equal("Days", r.GetProperty("rangeKind").GetString());
        Assert.Equal(60, r.GetProperty("days").GetInt32());
    }

    [Fact]
    public void Build_AllRange_UsesAllKind()
    {
        Assert.Equal("All", Json(SyncRequestBuilder.Build("All", "All", null, null)).GetProperty("rangeKind").GetString());
    }

    [Fact]
    public void Build_CustomRange_IncludesParsedDates()
    {
        JsonElement r = Json(SyncRequestBuilder.Build("All", "Custom", "2026-01-02", "2026-02-03"));
        Assert.Equal("Custom", r.GetProperty("rangeKind").GetString());
        Assert.StartsWith("2026-01-02", r.GetProperty("from").GetString());
        Assert.StartsWith("2026-02-03", r.GetProperty("to").GetString());
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
    public void FormatRecords_AllSixNull_ReturnsNull()
    {
        Assert.Null(SyncRequestBuilder.FormatRecords("{0}", null, null, null, null, null, null, "{0} snapshots"));
    }

    [Fact]
    public void FormatRecords_Counts_FormatsInOrder()
    {
        Assert.Equal("3/1/42/6/0", SyncRequestBuilder.FormatRecords("{0}/{1}/{2}/{3}/{4}", 3, 1, 42, 6, 0));
    }

    [Fact]
    public void DefaultCustomRange_Is30DaysBack()
    {
        (string? s, string? e) = SyncRequestBuilder.DefaultCustomRange(new DateTime(2026, 10, 17));
        Assert.Equal("2026-09-17", s);
        Assert.Equal("2026-10-17", e);
    }
}
