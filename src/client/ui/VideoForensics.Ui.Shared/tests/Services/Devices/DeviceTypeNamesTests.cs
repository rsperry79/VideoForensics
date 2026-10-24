using VideoForensics.Client.Ui.Shared.Services.Devices;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Services.Devices;

public class DeviceTypeNamesTests
{
    [Theory]
    [InlineData("doorbot", "Doorbell")]
    [InlineData("authorized_doorbot", "Doorbell")]
    [InlineData("stickup_cam", "Stick Up Cam")]
    [InlineData("chime", "Chime")]
    [InlineData("base_station", "Base Station")]
    [InlineData("range_extender", "Range Extender")]
    [InlineData("some_new_type", "Some New Type")]
    [InlineData(null, "Unknown")]
    public void Display_ReadableNames(string? type, string expected)
    {
        Assert.Equal(expected, DeviceTypeNames.Display(type));
    }

    [Theory]
    [InlineData("doorbot", true)]
    [InlineData("stickup_cam", true)]
    [InlineData("chime", false)]
    [InlineData("base_station", false)]
    [InlineData(null, false)]
    public void IsCamera_OnlyCameraTypes(string? type, bool expected)
    {
        Assert.Equal(expected, DeviceTypeNames.IsCamera(type));
    }
}
