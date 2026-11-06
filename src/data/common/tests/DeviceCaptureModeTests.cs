using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Data.Common.Tests;

/// <summary>The schedule page's three choices map onto the snapshot and health switches on each device.</summary>
public class DeviceCaptureModeTests
{
    private static Device NewDevice(bool snapshot = true, bool health = true)
    {
        return new()
        {
            ProviderDeviceId = "12345",
            Name = "Porch",
            Type = "doorbell",
            SnapshotCaptureEnabled = snapshot,
            HealthCaptureEnabled = health,
        };
    }

    [Fact]
    public void NewDevice_IsCapture()
    {
        Device device = NewDevice();

        Assert.Equal(DeviceCaptureMode.Capture, device.CaptureMode);
    }

    [Fact]
    public void SettingHealthOnly_KeepsHealthAndStopsSnapshots()
    {
        Device device = NewDevice();

        device.CaptureMode = DeviceCaptureMode.HealthOnly;

        Assert.False(device.SnapshotCaptureEnabled);
        Assert.True(device.HealthCaptureEnabled);
        Assert.Equal(DeviceCaptureMode.HealthOnly, device.CaptureMode);
    }

    [Fact]
    public void SettingOff_StopsSnapshotsAndHealth()
    {
        Device device = NewDevice();

        device.CaptureMode = DeviceCaptureMode.Off;

        Assert.False(device.SnapshotCaptureEnabled);
        Assert.False(device.HealthCaptureEnabled);
        Assert.Equal(DeviceCaptureMode.Off, device.CaptureMode);
    }

    [Fact]
    public void SettingCapture_TurnsBothOn()
    {
        Device device = NewDevice(snapshot: false, health: false);

        device.CaptureMode = DeviceCaptureMode.Capture;

        Assert.True(device.SnapshotCaptureEnabled);
        Assert.True(device.HealthCaptureEnabled);
    }

    [Fact]
    public void SnapshotsOnWithoutHealth_ReadsAsOff()
    {
        // Snapshots without health is not one of the three choices, so it reads as off rather than as capture.
        Device device = NewDevice(snapshot: true, health: false);

        Assert.Equal(DeviceCaptureMode.Off, device.CaptureMode);
    }
}
