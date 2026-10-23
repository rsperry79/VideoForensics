using VideoForensics.Data.Common.Entities;
using VideoForensics.Ui.Shared.Services.DeviceSettings;

using Xunit;

namespace VideoForensics.Ui.Shared.Tests;

public class RingSettingsRoute_Tests
{
    private static Device Dev(string? provider) => new() { Id = Guid.NewGuid(), LocationId = Guid.NewGuid(), ProviderDeviceId = "1", Name = "Front", Type = "doorbot", ProviderName = provider };

    [Fact]
    public void RingSettingsRoute_IsRingDevice_RingProvider_True()
    {
        Assert.True(RingSettingsRoute.IsRingDevice(Dev("Ring")));
    }

    [Theory]
    [InlineData("Wyze")]
    [InlineData(null)]
    public void RingSettingsRoute_IsRingDevice_OtherOrUnlinked_False(string? provider)
    {
        Assert.False(RingSettingsRoute.IsRingDevice(Dev(provider)));
    }

    [Fact]
    public void RingSettingsRoute_Href_BuildsDeviceRoute()
    {
        Guid id = Guid.Parse("12345678-1234-1234-1234-123456789abc");

        Assert.Equal("/devices/config/ring/12345678-1234-1234-1234-123456789abc", RingSettingsRoute.Href(id));
    }
}
