using VideoForensics.Client.Ui.Shared.Services;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>Which stored device types are cameras the viewers can play. Chimes, bridges and sensors are not.</summary>
public class DeviceKinds_Tests
{
    [Theory]
    [InlineData("camera")]
    [InlineData("Camera")]
    [InlineData("doorbell")]
    [InlineData("doorbell_v4")]
    [InlineData("doorbot")]
    [InlineData("authorized_doorbell")]
    [InlineData("stickup_cam")]
    [InlineData("stickup_cam_v4")]
    [InlineData("hp_cam_v2")]
    [InlineData("cocoa_camera")]
    [InlineData("cocoa_floodlight")]
    [InlineData("  doorbell  ")]
    public void IsCamera_CameraTypes_ReturnsTrue(string type)
    {
        Assert.True(DeviceKinds.IsCamera(type));
    }

    [Theory]
    [InlineData("chime")]
    [InlineData("chime_pro")]
    [InlineData("base_station")]
    [InlineData("alarm_hub")]
    [InlineData("keypad")]
    [InlineData("contact_sensor")]
    [InlineData("range_extender")]
    [InlineData("Unknown")]
    [InlineData("")]
    [InlineData(null)]
    public void IsCamera_NonCameraTypes_ReturnsFalse(string? type)
    {
        Assert.False(DeviceKinds.IsCamera(type));
    }
}
