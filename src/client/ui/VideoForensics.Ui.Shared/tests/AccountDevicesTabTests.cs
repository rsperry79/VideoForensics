namespace VideoForensics.Ui.Shared.Tests;

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Ui.Shared.Pages;
using Xunit;

/// <summary>The Devices tab: one row per device with its uptime status, and a warning when it has gone quiet.</summary>
public class AccountDevicesTabTests : AccountDetailsPageTestBase
{
    private readonly Location _location = new() { Id = Guid.NewGuid(), ProviderLocationId = "L", Name = "Home" };

    public AccountDevicesTabTests()
    {
        ForensicsConfigMock.Setup(c => c.DeviceOfflineThresholdSeconds).Returns(300);
        DeviceRepositoryMock.Setup(m => m.GetByLocationIdAsync(_location.Id, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Device>());
        LocationRepositoryMock.Setup(m => m.ListByProviderAccountIdAsync(AccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Location> { _location });
    }

    private IRenderedComponent<AccountDevicesTab> RenderDevices() =>
        Render<AccountDevicesTab>(p => p.Add(c => c.Id, AccountId));

    private Device AddDevice(string name)
    {
        var device = new Device { Id = Guid.NewGuid(), LocationId = _location.Id, ProviderDeviceId = name, Name = name, Type = "camera" };
        DeviceRepositoryMock.Setup(m => m.GetByLocationIdAsync(_location.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Device> { device });
        return device;
    }

    [Fact]
    public void DeviceWithNoRecentReading_ShowsOfflineWarning()
    {
        SetEmptyLists();
        var device = AddDevice("Porch");
        DeviceHealthRepositoryMock.Setup(m => m.GetLatestAsync(device.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeviceHealth { Id = Guid.NewGuid(), DeviceId = device.Id, CapturedAtUtc = DateTime.UtcNow.AddMinutes(-30) });

        var cut = RenderDevices();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid='device-offline-warning']")));
    }

    [Fact]
    public void DeviceWithFreshReading_HasNoOfflineWarning()
    {
        SetEmptyLists();
        var device = AddDevice("Porch");
        DeviceHealthRepositoryMock.Setup(m => m.GetLatestAsync(device.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeviceHealth { Id = Guid.NewGuid(), DeviceId = device.Id, CapturedAtUtc = DateTime.UtcNow });

        var cut = RenderDevices();

        cut.WaitForAssertion(() => Assert.Contains("Porch", cut.Find("#account-devices").TextContent));
        Assert.Empty(cut.FindAll("[data-testid='device-offline-warning']"));
    }

    [Fact]
    public void NoDevices_ShowsNoDevicesMessage()
    {
        SetEmptyLists();

        var cut = RenderDevices();

        cut.WaitForAssertion(() => Assert.Contains(Strings["DevicesNone"], cut.Markup));
    }
}
