using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Data.Common.Tests;

public class SnapshotCaptureEntityTests
{
    private static readonly DateTimeOffset CapturedAt = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);
    private const string MediaPath = "Devices/abc/2026-10-18T12-00-00Z.jpg";

    [Fact]
    public void SnapshotCapture_Create_Status200_IsCurrentWithMediaPath()
    {
        var deviceId = Guid.NewGuid();
        var healthId = Guid.NewGuid();

        var capture = SnapshotCapture.Create(deviceId, CapturedAt, "fresh", 200, MediaPath, healthId);

        Assert.NotEqual(Guid.Empty, capture.Id);
        Assert.Equal(deviceId, capture.DeviceId);
        Assert.Equal(CapturedAt, capture.CapturedAtUtc);
        Assert.Equal("fresh", capture.Trigger);
        Assert.Equal(200, capture.HttpStatus);
        Assert.True(capture.IsCurrent);
        Assert.Equal(MediaPath, capture.MediaFilePath);
        Assert.Equal(healthId, capture.DeviceHealthId);
    }

    [Fact]
    public void SnapshotCapture_Create_Status404_NotCurrentWithoutMediaPath()
    {
        var capture = SnapshotCapture.Create(Guid.NewGuid(), CapturedAt, "latest", 404, null, null);

        Assert.Equal(404, capture.HttpStatus);
        Assert.False(capture.IsCurrent);
        Assert.Null(capture.MediaFilePath);
        Assert.Null(capture.DeviceHealthId);
    }

    [Fact]
    public void SnapshotCapture_Create_Status404WithMediaPath_Throws()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            SnapshotCapture.Create(Guid.NewGuid(), CapturedAt, "fresh", 404, MediaPath, null));
    }

    [Fact]
    public void SnapshotCapture_Create_Status200WithoutMediaPath_Throws()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            SnapshotCapture.Create(Guid.NewGuid(), CapturedAt, "fresh", 200, null, null));
    }

    [Theory]
    [InlineData("Fresh")]
    [InlineData("manual")]
    [InlineData("")]
    public void SnapshotCapture_Create_UnknownTrigger_Throws(string trigger)
    {
        _ = Assert.Throws<ArgumentException>(() =>
            SnapshotCapture.Create(Guid.NewGuid(), CapturedAt, trigger, 200, MediaPath, null));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(600)]
    public void SnapshotCapture_Create_StatusOutOfRange_Throws(int httpStatus)
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            SnapshotCapture.Create(Guid.NewGuid(), CapturedAt, "fresh", httpStatus, null, null));
    }

    [Fact]
    public void SnapshotCapture_Create_NonUtcOffset_StoresUtcInstant()
    {
        var localTime = new DateTimeOffset(2026, 10, 18, 8, 0, 0, TimeSpan.FromHours(-4));

        var capture = SnapshotCapture.Create(Guid.NewGuid(), localTime, "fresh", 200, MediaPath, null);

        Assert.Equal(TimeSpan.Zero, capture.CapturedAtUtc.Offset);
        Assert.Equal(localTime.UtcDateTime, capture.CapturedAtUtc.UtcDateTime);
    }
}
