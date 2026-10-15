namespace VideoForensics.Ui.Shared.Tests.Components.Evidence;

using Bunit;
using Xunit;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Ui.Shared.Components.Evidence;
using VideoForensics.Ui.Shared.Services.Evidence;

public class SnapshotStripTests : BunitContext
{
    private static readonly DateTime RangeStart = new(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime RangeEnd = new(2026, 9, 20, 10, 0, 0, DateTimeKind.Utc);

    private static (List<EvidenceItem> Items, Dictionary<Guid, string> Urls) BuildItems(Guid deviceId)
    {
        var media1 = new MediaItem { Id = Guid.NewGuid(), DeviceId = deviceId, FileName = "a.jpg", FilePath = "/a.jpg", MediaFormat = "image/jpeg", Sha256Hash = "h1", RecordedAtUtc = RangeStart.AddHours(1), DownloadedAtUtc = RangeStart.AddHours(1) };
        var media2 = new MediaItem { Id = Guid.NewGuid(), DeviceId = deviceId, FileName = "b.jpg", FilePath = "/b.jpg", MediaFormat = "image/jpeg", Sha256Hash = "h2", RecordedAtUtc = RangeStart.AddHours(2), DownloadedAtUtc = RangeStart.AddHours(2) };

        var items = new List<EvidenceItem>
        {
            new() { Key = $"media:{media1.Id}", Kind = EvidenceKind.Snapshot, OccurredAtUtc = media1.RecordedAtUtc, DeviceId = deviceId, DeviceName = "Front Door", Media = media1 },
            new() { Key = $"media:{media2.Id}", Kind = EvidenceKind.Snapshot, OccurredAtUtc = media2.RecordedAtUtc, DeviceId = deviceId, DeviceName = "Front Door", Media = media2 },
        };

        var urls = new Dictionary<Guid, string>
        {
            [media1.Id] = "https://cdn.test/a.jpg?ticket=1",
            [media2.Id] = "https://cdn.test/b.jpg?ticket=2",
        };

        return (items, urls);
    }

    [Fact]
    public void Renders_OneThumbnailPerItem_WithTicketedUrl()
    {
        var (items, urls) = BuildItems(Guid.NewGuid());

        var component = Render<SnapshotStrip>(p => p
            .Add(x => x.Items, items)
            .Add(x => x.ThumbnailUrls, urls)
            .Add(x => x.RangeStartUtc, RangeStart)
            .Add(x => x.RangeEndUtc, RangeEnd));

        var thumbs = component.FindAll("[data-testid='snapshot-strip-thumb']");
        Assert.Equal(2, thumbs.Count);

        var img = component.Find($"[data-key='{items[0].Key}'] img");
        Assert.Equal(urls[items[0].Media!.Id], img.GetAttribute("src"));
    }

    [Fact]
    public void ClickThumbnail_InvokesOnSelect_AndUpdatesSelectedIndex()
    {
        var (items, urls) = BuildItems(Guid.NewGuid());
        EvidenceItem? selected = null;
        int? selectedIndex = null;

        var component = Render<SnapshotStrip>(p => p
            .Add(x => x.Items, items)
            .Add(x => x.ThumbnailUrls, urls)
            .Add(x => x.RangeStartUtc, RangeStart)
            .Add(x => x.RangeEndUtc, RangeEnd)
            .Add(x => x.SelectedIndex, 0)
            .Add(x => x.SelectedIndexChanged, i => selectedIndex = i)
            .Add(x => x.OnSelect, item => selected = item));

        component.Find($"[data-key='{items[1].Key}']").Click();

        Assert.Equal(items[1], selected);
        Assert.Equal(1, selectedIndex);
    }

    [Fact]
    public void ArrowRightKey_MovesSelectionForward()
    {
        var (items, urls) = BuildItems(Guid.NewGuid());
        int? selectedIndex = null;

        var component = Render<SnapshotStrip>(p => p
            .Add(x => x.Items, items)
            .Add(x => x.ThumbnailUrls, urls)
            .Add(x => x.RangeStartUtc, RangeStart)
            .Add(x => x.RangeEndUtc, RangeEnd)
            .Add(x => x.SelectedIndex, 0)
            .Add(x => x.SelectedIndexChanged, i => selectedIndex = i));

        component.Find("[data-testid='snapshot-strip']").KeyDown("ArrowRight");

        Assert.Equal(1, selectedIndex);
    }

    [Fact]
    public void ArrowLeftKey_AtStart_DoesNotGoNegative()
    {
        var (items, urls) = BuildItems(Guid.NewGuid());
        int? selectedIndex = null;

        var component = Render<SnapshotStrip>(p => p
            .Add(x => x.Items, items)
            .Add(x => x.ThumbnailUrls, urls)
            .Add(x => x.RangeStartUtc, RangeStart)
            .Add(x => x.RangeEndUtc, RangeEnd)
            .Add(x => x.SelectedIndex, 0)
            .Add(x => x.SelectedIndexChanged, i => selectedIndex = i));

        component.Find("[data-testid='snapshot-strip']").KeyDown("ArrowLeft");

        Assert.Null(selectedIndex);
    }

    [Fact]
    public void ArrowRightKey_AtEnd_DoesNotExceedLastIndex()
    {
        var (items, urls) = BuildItems(Guid.NewGuid());
        int? selectedIndex = null;

        var component = Render<SnapshotStrip>(p => p
            .Add(x => x.Items, items)
            .Add(x => x.ThumbnailUrls, urls)
            .Add(x => x.RangeStartUtc, RangeStart)
            .Add(x => x.RangeEndUtc, RangeEnd)
            .Add(x => x.SelectedIndex, items.Count - 1)
            .Add(x => x.SelectedIndexChanged, i => selectedIndex = i));

        component.Find("[data-testid='snapshot-strip']").KeyDown("ArrowRight");

        Assert.Null(selectedIndex);
    }

    [Fact]
    public void NoItems_DoesNotRenderScrubber()
    {
        var component = Render<SnapshotStrip>(p => p
            .Add(x => x.Items, new List<EvidenceItem>())
            .Add(x => x.RangeStartUtc, RangeStart)
            .Add(x => x.RangeEndUtc, RangeEnd));

        Assert.Empty(component.FindAll("[data-testid='snapshot-strip-scrubber']"));
    }

    [Fact]
    public void RssiSamples_RenderPolyline()
    {
        var (items, urls) = BuildItems(Guid.NewGuid());
        var rssi = new List<RssiSample> { new(RangeStart.AddHours(1), -60), new(RangeStart.AddHours(2), -55) };

        var component = Render<SnapshotStrip>(p => p
            .Add(x => x.Items, items)
            .Add(x => x.ThumbnailUrls, urls)
            .Add(x => x.RangeStartUtc, RangeStart)
            .Add(x => x.RangeEndUtc, RangeEnd)
            .Add(x => x.RssiSamples, rssi));

        var polyline = component.Find("[data-testid='snapshot-strip-rssi'] polyline");
        Assert.False(string.IsNullOrWhiteSpace(polyline.GetAttribute("points")));
    }

    [Fact]
    public void SelectedIndex_RendersScrubPositionMarker()
    {
        var (items, urls) = BuildItems(Guid.NewGuid());

        var component = Render<SnapshotStrip>(p => p
            .Add(x => x.Items, items)
            .Add(x => x.ThumbnailUrls, urls)
            .Add(x => x.RangeStartUtc, RangeStart)
            .Add(x => x.RangeEndUtc, RangeEnd)
            .Add(x => x.SelectedIndex, 1));

        Assert.NotEmpty(component.FindAll("[data-testid='snapshot-strip-rssi-marker']"));
    }
}
