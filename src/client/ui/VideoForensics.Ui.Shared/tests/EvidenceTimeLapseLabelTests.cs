using VideoForensics.Data.Common.Entities;
using VideoForensics.Client.Ui.Shared.Services.Evidence;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>A roll-up loop (snapshots built into an MP4) is labelled Time Lapse; other videos stay Video.</summary>
public class EvidenceTimeLapseLabelTests
{
    private static EvidenceItem Video(string? metadata)
    {
        return new()
        {
            Key = "k",
            Kind = EvidenceKind.Video,
            OccurredAtUtc = DateTime.UtcNow,
            DeviceId = Guid.NewGuid(),
            DeviceName = "Porch",
            Media = new MediaItem
            {
                Id = Guid.NewGuid(),
                DeviceId = Guid.Empty,
                FileName = "loop.mp4",
                FilePath = "loop.mp4",
                MediaFormat = "mp4",
                Sha256Hash = "h",
                RecordedAtUtc = DateTime.UtcNow,
                DownloadedAtUtc = DateTime.UtcNow,
                MetadataJson = metadata,
            },
        };
    }

    [Fact]
    public void RollUpLoop_IsLabelledTimeLapse()
    {
        EvidenceItem item = Video("{\"type\":\"snapshot-rollup\"}");

        Assert.True(item.IsTimeLapse);
        Assert.Equal("Time Lapse", item.Label);
    }

    [Fact]
    public void EventVideo_StaysLabelledVideo()
    {
        EvidenceItem item = Video("{\"eventId\":\"e1\"}");

        Assert.False(item.IsTimeLapse);
        Assert.Equal("Video", item.Label);
    }

    [Fact]
    public void VideoWithoutMetadata_StaysLabelledVideo()
    {
        Assert.Equal("Video", Video(null).Label);
    }
}
