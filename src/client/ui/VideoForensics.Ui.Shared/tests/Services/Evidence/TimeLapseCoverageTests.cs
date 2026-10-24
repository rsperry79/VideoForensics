using VideoForensics.Data.Common.Entities;
using VideoForensics.Client.Ui.Shared.Services.Evidence;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Services.Evidence;

/// <summary>A Time Lapse hides the snapshots of its device inside its window, and only in the view.</summary>
public class TimeLapseCoverageTests
{
    private static readonly DateTime End = new(2026, 10, 20, 12, 15, 0, DateTimeKind.Utc);
    private static readonly Guid Device = Guid.NewGuid();

    private static EvidenceItem Snapshot(Guid device, DateTime at)
    {
        return new()
        {
            Key = Guid.NewGuid().ToString(),
            Kind = EvidenceKind.Snapshot,
            OccurredAtUtc = at,
            DeviceId = device,
            DeviceName = "Porch",
        };
    }

    private static EvidenceItem Lapse(Guid device, DateTime recordedAt)
    {
        return new()
        {
            Key = Guid.NewGuid().ToString(),
            Kind = EvidenceKind.Video,
            OccurredAtUtc = recordedAt,
            DeviceId = device,
            DeviceName = "Porch",
            Media = new MediaItem
            {
                Id = Guid.NewGuid(),
                DeviceId = device,
                FileName = "loop.mp4",
                FilePath = "loop.mp4",
                MediaFormat = "mp4",
                Sha256Hash = "h",
                RecordedAtUtc = recordedAt,
                DownloadedAtUtc = recordedAt,
                MetadataJson = "{\"type\":\"snapshot-rollup\"}",
            },
        };
    }

    [Fact]
    public void SnapshotsInsideTheLapseWindow_AreHidden()
    {
        EvidenceItem inside = Snapshot(Device, End.AddMinutes(-5));
        EvidenceItem lapse = Lapse(Device, End);

        IReadOnlyList<EvidenceItem> shown = TimeLapseCoverage.HideCovered(new[] { inside, lapse }, _ => TimeSpan.FromMinutes(15));

        Assert.DoesNotContain(inside, shown);
        Assert.Contains(lapse, shown);
    }

    [Fact]
    public void SnapshotsOutsideTheLapseWindow_StayVisible()
    {
        EvidenceItem before = Snapshot(Device, End.AddMinutes(-20));
        EvidenceItem lapse = Lapse(Device, End);

        IReadOnlyList<EvidenceItem> shown = TimeLapseCoverage.HideCovered(new[] { before, lapse }, _ => TimeSpan.FromMinutes(15));

        Assert.Contains(before, shown);
    }

    [Fact]
    public void OtherDevicesSnapshots_StayVisible()
    {
        EvidenceItem other = Snapshot(Guid.NewGuid(), End.AddMinutes(-5));
        EvidenceItem lapse = Lapse(Device, End);

        IReadOnlyList<EvidenceItem> shown = TimeLapseCoverage.HideCovered(new[] { other, lapse }, _ => TimeSpan.FromMinutes(15));

        Assert.Contains(other, shown);
    }

    [Fact]
    public void NoTimeLapse_ChangesNothing()
    {
        EvidenceItem snap = Snapshot(Device, End.AddMinutes(-5));

        IReadOnlyList<EvidenceItem> shown = TimeLapseCoverage.HideCovered(new[] { snap }, _ => TimeSpan.FromMinutes(15));

        _ = Assert.Single(shown);
    }
}
