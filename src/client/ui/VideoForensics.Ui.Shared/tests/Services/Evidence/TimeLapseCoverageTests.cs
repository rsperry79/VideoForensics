using VideoForensics.Client.Ui.Shared.Services.Evidence;using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Services.Evidence;

/// <summary>
/// A time lapse hides the snapshots of its device that fall inside its own span, [its start, its start + its duration).
/// Spans vary in length, so the span comes from the row, not from a fixed window. Nothing is deleted.
/// </summary>
public class TimeLapseCoverageTests
{
    private static readonly DateTime Start = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);
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

    private static EvidenceItem Lapse(Guid device, DateTime recordedAt, TimeSpan span)
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
                DurationSeconds = span.TotalSeconds,
                DownloadedAtUtc = recordedAt,
                MetadataJson = "{\"type\":\"snapshot-rollup\"}",
            }.ToDto(),
        };
    }

    [Fact]
    public void SnapshotsInsideAVariableLengthSpan_AreHidden()
    {
        // A 43-minute time lapse covers 12:00 to 12:43, so a snapshot at 12:30 is hidden.
        EvidenceItem inside = Snapshot(Device, Start.AddMinutes(30));
        EvidenceItem lapse = Lapse(Device, Start, TimeSpan.FromMinutes(43));

        IReadOnlyList<EvidenceItem> shown = TimeLapseCoverage.HideCovered(new[] { inside, lapse });

        Assert.DoesNotContain(inside, shown);
        Assert.Contains(lapse, shown);
    }

    [Fact]
    public void SnapshotAfterTheSpanEnd_StaysVisible()
    {
        // The same 43-minute time lapse does not cover 12:50.
        EvidenceItem after = Snapshot(Device, Start.AddMinutes(50));
        EvidenceItem lapse = Lapse(Device, Start, TimeSpan.FromMinutes(43));

        IReadOnlyList<EvidenceItem> shown = TimeLapseCoverage.HideCovered(new[] { after, lapse });

        Assert.Contains(after, shown);
    }

    [Fact]
    public void SnapshotBeforeTheSpanStart_StaysVisible()
    {
        EvidenceItem before = Snapshot(Device, Start.AddMinutes(-5));
        EvidenceItem lapse = Lapse(Device, Start, TimeSpan.FromMinutes(43));

        IReadOnlyList<EvidenceItem> shown = TimeLapseCoverage.HideCovered(new[] { before, lapse });

        Assert.Contains(before, shown);
    }

    [Fact]
    public void OtherDevicesSnapshots_StayVisible()
    {
        EvidenceItem other = Snapshot(Guid.NewGuid(), Start.AddMinutes(30));
        EvidenceItem lapse = Lapse(Device, Start, TimeSpan.FromMinutes(43));

        IReadOnlyList<EvidenceItem> shown = TimeLapseCoverage.HideCovered(new[] { other, lapse });

        Assert.Contains(other, shown);
    }

    [Fact]
    public void NoTimeLapse_ChangesNothing()
    {
        EvidenceItem snap = Snapshot(Device, Start.AddMinutes(30));

        IReadOnlyList<EvidenceItem> shown = TimeLapseCoverage.HideCovered(new[] { snap });

        _ = Assert.Single(shown);
    }
}
