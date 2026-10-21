namespace VideoForensics.Ui.Shared.Tests;

using Bunit;
using VideoForensics.Client.Common.Contracts;
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
        MediaItemRepositoryMock.Setup(m => m.GetRecentByDeviceAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<MediaItem>());
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
    public void DeviceWithSnapshotAndLoop_ShowsOnlyTheLatestOfEach()
    {
        SetEmptyLists();
        var device = AddDevice("Porch");
        var snapshot = new MediaItem { Id = Guid.NewGuid(), DeviceId = device.Id, FileName = "s.jpg", FilePath = "s", MediaFormat = "jpg", RecordedAtUtc = DateTime.UtcNow.AddMinutes(-1), Sha256Hash = "h", MetadataJson = "{\"type\":\"snapshot\"}" };
        var loop = new MediaItem { Id = Guid.NewGuid(), DeviceId = device.Id, FileName = "l.mp4", FilePath = "l", MediaFormat = "mp4", RecordedAtUtc = DateTime.UtcNow.AddMinutes(-2), Sha256Hash = "h", MetadataJson = "{\"type\":\"snapshot-rollup\"}" };
        MediaItemRepositoryMock.Setup(m => m.GetRecentByDeviceAsync(device.Id, It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(new List<MediaItem> { snapshot, loop });
        MediaUrlProviderMock.Setup(m => m.GetContentUrlsAsync(It.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 2), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [snapshot.Id] = "/snap.jpg", [loop.Id] = "/loop.mp4" });

        var cut = RenderDevices();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("td.latest-snapshot img[src='/snap.jpg']")));
        Assert.Single(cut.FindAll("td.latest-loop video[src='/loop.mp4']"));
    }


    [Fact]
    public void NoDevices_ShowsNoDevicesMessage()
    {
        SetEmptyLists();

        var cut = RenderDevices();

        cut.WaitForAssertion(() => Assert.Contains(Strings["DevicesNone"], cut.Markup));
    }
}
