using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Data.Common.Tests;

public class SnapshotCaptureSkipEntityTests
{
    private static readonly DateTimeOffset CapturedAt = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);
    private const string MediaPath = "Devices/abc/2026-10-18T12-00-00Z.jpg";

    [Fact]
    public void SnapshotCapture_Create_SkipWithMediaPath_Throws()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            SnapshotCapture.Create(Guid.NewGuid(), CapturedAt, "fresh", 200, MediaPath, null, SnapshotSkipReason.IdenticalFrame));
    }

    [Fact]
    public void SnapshotCapture_Create_SkipWithNullMediaPath_KeepsRealStatusAndHasNoMedia()
    {
        var capture = SnapshotCapture.Create(Guid.NewGuid(), CapturedAt, "fresh", 200, null, null, SnapshotSkipReason.Offline);

        Assert.Equal(200, capture.HttpStatus);
        Assert.False(capture.IsCurrent);
        Assert.Null(capture.MediaFilePath);
        Assert.Equal(SnapshotSkipReason.Offline, capture.SkipReason);
    }

    [Fact]
    public void SnapshotCapture_Create_NormalAttempt_SkipReasonIsNull()
    {
        var capture = SnapshotCapture.Create(Guid.NewGuid(), CapturedAt, "fresh", 200, MediaPath, null);

        Assert.Null(capture.SkipReason);
    }
}
