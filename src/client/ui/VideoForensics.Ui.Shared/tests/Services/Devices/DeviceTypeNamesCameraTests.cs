using VideoForensics.Client.Ui.Shared.Services.Devices;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Services.Devices;

/// <summary>Snapshots and time lapses exist only for cameras, so every Ring doorbell kind has to count as one.</summary>
public class DeviceTypeNamesCameraTests
{
    [Theory]
    [InlineData("doorbot")]
    [InlineData("authorized_doorbot")]
    [InlineData("stickup_cam")]
    [InlineData("doorbell")]
    [InlineData("doorbell_v3")]
    [InlineData("doorbell_v5")]
    [InlineData("doorbell_portal")]
    public void IsCamera_TrueForCameraAndDoorbellKinds(string type)
    {
        Assert.True(DeviceTypeNames.IsCamera(type));
    }

    [Theory]
    [InlineData("chime")]
    [InlineData("base_station_v1")]
    [InlineData("")]
    [InlineData(null)]
    public void IsCamera_FalseForOtherDevices(string? type)
    {
        Assert.False(DeviceTypeNames.IsCamera(type));
    }
}
