using VideoForensics.Client.Ui.Shared.Services.DeviceSettings;using VideoForensics.Core.Api.Contracts;
using Device = VideoForensics.Core.Api.Contracts.DeviceDto;
using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Services.DeviceSettings;

public class RingSettingsRoute_Tests
{
    private static Device Dev(string? provider)
    {
        return new VideoForensics.Data.Common.Entities.Device() { Id = Guid.NewGuid(), LocationId = Guid.NewGuid(), ProviderDeviceId = "1", Name = "Front", Type = "doorbot", ProviderName = provider }.ToDto();
    }

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
        var id = Guid.Parse("12345678-1234-1234-1234-123456789abc");

        Assert.Equal("/devices/config/ring/12345678-1234-1234-1234-123456789abc", RingSettingsRoute.Href(id));
    }
}
