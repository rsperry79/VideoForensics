
using AngleSharp.Dom;

using Bunit;

using Microsoft.Extensions.DependencyInjection;

using VideoForensics.Ui.Shared.Components.Evidence;
using VideoForensics.Ui.Shared.Services.Evidence;

using Xunit;

namespace VideoForensics.Ui.Shared.Tests.Components.Evidence;

public class DeviceTimeGridViewTests : BunitContext
{
    public DeviceTimeGridViewTests()
    {
        _ = Services.AddLocalization();
    }

    private static DeviceTimeGridResult BuildResult(out Guid deviceAId, out Guid deviceBId)
    {
        deviceAId = Guid.NewGuid();
        deviceBId = Guid.NewGuid();

        var bucketStarts = new List<DateTime>
        {
            new(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc),
            new(2026, 9, 20, 1, 0, 0, DateTimeKind.Utc),
            new(2026, 9, 20, 2, 0, 0, DateTimeKind.Utc),
        };

        var rowA = new DeviceTimeRow(deviceAId, "Front Door",
        [
            new(bucketStarts[0], bucketStarts[0].AddHours(1), EventCount: 2, SnapshotCount: 0, IsGap: false),
            new(bucketStarts[1], bucketStarts[1].AddHours(1), EventCount: 0, SnapshotCount: 0, IsGap: true),
            new(bucketStarts[2], bucketStarts[2].AddHours(1), EventCount: 0, SnapshotCount: 1, IsGap: false),
        ]);

        var rowB = new DeviceTimeRow(deviceBId, "Back Yard",
        [
            new(bucketStarts[0], bucketStarts[0].AddHours(1), EventCount: 0, SnapshotCount: 0, IsGap: false),
            new(bucketStarts[1], bucketStarts[1].AddHours(1), EventCount: 0, SnapshotCount: 0, IsGap: false),
            new(bucketStarts[2], bucketStarts[2].AddHours(1), EventCount: 0, SnapshotCount: 0, IsGap: false),
        ]);

        return new DeviceTimeGridResult(bucketStarts, TimeSpan.FromHours(1), [rowA, rowB]);
    }

    [Fact]
    public void Renders_OneColumnHeaderPerBucket_AndOneRowPerDevice()
    {
        DeviceTimeGridResult result = BuildResult(out _, out _);

        IRenderedComponent<DeviceTimeGridView> component = Render<DeviceTimeGridView>(p => p.Add(x => x.Result, result));

        IReadOnlyList<IElement> headers = component.FindAll(".device-time-grid-col-header");
        Assert.Equal(3, headers.Count);

        IReadOnlyList<IElement> rows = component.FindAll("tbody tr");
        Assert.Equal(2, rows.Count);
        Assert.Contains("Front Door", rows[0].TextContent);
        Assert.Contains("Back Yard", rows[1].TextContent);
    }

    [Fact]
    public void GapCell_HasGapCssClass()
    {
        DeviceTimeGridResult result = BuildResult(out Guid deviceAId, out _);

        IRenderedComponent<DeviceTimeGridView> component = Render<DeviceTimeGridView>(p => p.Add(x => x.Result, result));

        IElement row = component.Find($"tr[data-key='{deviceAId}']");
        IHtmlCollection<IElement> cells = row.QuerySelectorAll("td");
        Assert.Contains("gap", cells[1].ClassName);
        Assert.DoesNotContain("gap", cells[0].ClassName);
    }

    [Fact]
    public void ActiveCell_ShowsEventAndMediaMarkers()
    {
        DeviceTimeGridResult result = BuildResult(out Guid deviceAId, out _);

        IRenderedComponent<DeviceTimeGridView> component = Render<DeviceTimeGridView>(p => p.Add(x => x.Result, result));

        IElement row = component.Find($"tr[data-key='{deviceAId}']");
        IHtmlCollection<IElement> cells = row.QuerySelectorAll("td");
        Assert.Contains("2", cells[0].TextContent);
        Assert.Contains("1", cells[2].TextContent);
    }

    [Fact]
    public void ClickCell_InvokesOnCellClick_WithDeviceAndBucketRange()
    {
        DeviceTimeGridResult result = BuildResult(out Guid deviceAId, out _);
        DeviceTimeCellClickArgs? captured = null;

        IRenderedComponent<DeviceTimeGridView> component = Render<DeviceTimeGridView>(p => p
            .Add(x => x.Result, result)
            .Add(x => x.OnCellClick, args => captured = args));

        IElement row = component.Find($"tr[data-key='{deviceAId}']");
        row.QuerySelectorAll("td")[0].Click();

        Assert.NotNull(captured);
        Assert.Equal(deviceAId, captured!.DeviceId);
        Assert.Equal(result.BucketStartsUtc[0], captured.BucketStartUtc);
    }

    [Fact]
    public void ClickDeviceRowHeader_InvokesOnDeviceSelected()
    {
        DeviceTimeGridResult result = BuildResult(out Guid deviceAId, out _);
        Guid? selected = null;

        IRenderedComponent<DeviceTimeGridView> component = Render<DeviceTimeGridView>(p => p
            .Add(x => x.Result, result)
            .Add(x => x.OnDeviceSelected, id => selected = id));

        component.Find($"tr[data-key='{deviceAId}'] .device-time-grid-row-header").Click();

        Assert.Equal(deviceAId, selected);
    }

    [Fact]
    public void SelectedDeviceId_MarksMatchingRowHeaderAsSelected()
    {
        DeviceTimeGridResult result = BuildResult(out Guid deviceAId, out _);

        IRenderedComponent<DeviceTimeGridView> component = Render<DeviceTimeGridView>(p => p
            .Add(x => x.Result, result)
            .Add(x => x.SelectedDeviceId, deviceAId));

        IElement header = component.Find($"tr[data-key='{deviceAId}'] .device-time-grid-row-header");
        Assert.Contains("selected", header.ClassName);
    }
}
