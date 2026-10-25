using Moq;
using Microsoft.AspNetCore.Http;

using VideoForensics.Client.WebApp.Api;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Client.WebApp.Tests.Api;

/// <summary>The schedule page lists an account's devices with their capture mode, and saves one device's mode.</summary>
public class DeviceCaptureModeEndpointsTests
{
    private static readonly Guid AccountId = Guid.NewGuid();

    private static Device Dev(string providerId, string name, DeviceCaptureMode mode)
    {
        var device = new Device { ProviderDeviceId = providerId, Name = name, Type = "doorbell" };
        device.CaptureMode = mode;
        return device;
    }

    [Fact]
    public async Task List_ReturnsAccountDevicesWithModesInNameOrder()
    {
        var location = new Location { Id = Guid.NewGuid(), ProviderLocationId = "L", Name = "Home" };
        var locations = new Mock<ILocationRepository>();
        _ = locations.Setup(l => l.ListByProviderAccountIdAsync(AccountId, It.IsAny<CancellationToken>())).ReturnsAsync([location]);
        var devices = new Mock<IDeviceRepository>();
        _ = devices.Setup(d => d.GetByLocationIdAsync(location.Id, It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            Dev("2", "Porch", DeviceCaptureMode.HealthOnly),
            Dev("1", "Driveway", DeviceCaptureMode.Capture),
        ]);

        IReadOnlyList<DeviceCaptureModeDto> rows = await DeviceCaptureModeEndpoints.ListForAccountAsync(AccountId, locations.Object, devices.Object, CancellationToken.None);

        Assert.Equal(new[] { "Driveway", "Porch" }, rows.Select(r => r.Name).ToArray());
        Assert.Equal(new[] { "Capture", "HealthOnly" }, rows.Select(r => r.Mode).ToArray());
    }

    [Fact]
    public async Task Set_SavesTheModeOnTheDevice()
    {
        Device device = Dev("1", "Porch", DeviceCaptureMode.Capture);
        var devices = new Mock<IDeviceRepository>();
        _ = devices.Setup(d => d.GetAsync(device.Id, It.IsAny<CancellationToken>())).ReturnsAsync(device);

        await DeviceCaptureModeEndpoints.SetModeAsync(device.Id, new SetDeviceCaptureModeRequest("off"), devices.Object, CancellationToken.None);

        Assert.Equal(DeviceCaptureMode.Off, device.CaptureMode);
        devices.Verify(d => d.UpdateAsync(device, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Set_UnknownMode_IsRejectedAndNothingIsSaved()
    {
        Device device = Dev("1", "Porch", DeviceCaptureMode.Capture);
        var devices = new Mock<IDeviceRepository>();
        _ = devices.Setup(d => d.GetAsync(device.Id, It.IsAny<CancellationToken>())).ReturnsAsync(device);

        _ = await DeviceCaptureModeEndpoints.SetModeAsync(device.Id, new SetDeviceCaptureModeRequest("Everything"), devices.Object, CancellationToken.None);

        Assert.Equal(DeviceCaptureMode.Capture, device.CaptureMode);
        devices.Verify(d => d.UpdateAsync(It.IsAny<Device>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Set_UnknownDevice_ReturnsNotFound()
    {
        var devices = new Mock<IDeviceRepository>();
        _ = devices.Setup(d => d.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((Device?)null);

        IResult result = await DeviceCaptureModeEndpoints.SetModeAsync(Guid.NewGuid(), new SetDeviceCaptureModeRequest("Off"), devices.Object, CancellationToken.None);

        Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.NotFound>(result);
    }
}
