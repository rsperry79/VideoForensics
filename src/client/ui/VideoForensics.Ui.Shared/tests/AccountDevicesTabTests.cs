
using Bunit;

using Moq;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Client.Ui.Shared.Pages;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;
/// <summary>The Devices tab: one row per device with its uptime status, and a warning when it has gone quiet.</summary>
public class AccountDevicesTabTests : AccountDetailsPageTestBase
{
    private readonly Location _location = new() { Id = Guid.NewGuid(), ProviderLocationId = "L", Name = "Home" };

    public AccountDevicesTabTests()
    {
        _ = ForensicsConfigMock.Setup(c => c.DeviceOfflineThresholdSeconds).Returns(300);
        _ = MediaItemRepositoryMock.Setup(m => m.GetRecentByDeviceAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _ = DeviceRepositoryMock.Setup(m => m.GetByLocationIdAsync(_location.Id, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _ = LocationRepositoryMock.Setup(m => m.ListByProviderAccountIdAsync(AccountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([_location]);
    }

    private IRenderedComponent<AccountDevicesTab> RenderDevices()
    {
        return Render<AccountDevicesTab>(p => p.Add(c => c.Id, AccountId));
    }

    private Device AddDevice(string name)
    {
        var device = new Device { Id = Guid.NewGuid(), LocationId = _location.Id, ProviderDeviceId = name, Name = name, Type = "stickup_cam" };
        _ = DeviceRepositoryMock.Setup(m => m.GetByLocationIdAsync(_location.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([device]);
        return device;
    }

    [Fact]
    public void DeviceWithNoRecentReading_ShowsOfflineWarning()
    {
        SetEmptyLists();
        Device device = AddDevice("Porch");
        _ = DeviceHealthRepositoryMock.Setup(m => m.GetLatestAsync(device.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeviceHealth { Id = Guid.NewGuid(), DeviceId = device.Id, CapturedAtUtc = DateTime.UtcNow.AddMinutes(-30) });

        IRenderedComponent<AccountDevicesTab> cut = RenderDevices();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid='device-offline-warning']")));
    }

    [Fact]
    public void DeviceWithFreshReading_HasNoOfflineWarning()
    {
        SetEmptyLists();
        Device device = AddDevice("Porch");
        _ = DeviceHealthRepositoryMock.Setup(m => m.GetLatestAsync(device.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DeviceHealth { Id = Guid.NewGuid(), DeviceId = device.Id, CapturedAtUtc = DateTime.UtcNow });

        IRenderedComponent<AccountDevicesTab> cut = RenderDevices();

        cut.WaitForAssertion(() => Assert.Contains("Porch", cut.Find("#account-devices").TextContent));
        Assert.Empty(cut.FindAll("[data-testid='device-offline-warning']"));
    }
    [Fact]
    public void DeviceWithSnapshotAndLoop_ShowsOnlyTheLatestOfEach()
    {
        SetEmptyLists();
        Device device = AddDevice("Porch");
        var snapshot = new MediaItem { Id = Guid.NewGuid(), DeviceId = device.Id, FileName = "s.jpg", FilePath = "s", MediaFormat = "jpg", RecordedAtUtc = DateTime.UtcNow.AddMinutes(-1), Sha256Hash = "h", MetadataJson = "{\"type\":\"snapshot\"}" };
        var loop = new MediaItem { Id = Guid.NewGuid(), DeviceId = device.Id, FileName = "l.mp4", FilePath = "l", MediaFormat = "mp4", RecordedAtUtc = DateTime.UtcNow.AddMinutes(-2), Sha256Hash = "h", MetadataJson = "{\"type\":\"snapshot-rollup\"}" };
        _ = MediaItemRepositoryMock.Setup(m => m.GetRecentByDeviceAsync(device.Id, It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([snapshot, loop]);
        _ = MediaUrlProviderMock.Setup(m => m.GetContentUrlsAsync(It.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 2), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [snapshot.Id] = "/snap.jpg", [loop.Id] = "/loop.mp4" });

        IRenderedComponent<AccountDevicesTab> cut = RenderDevices();

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("td.latest-snapshot img[src='/snap.jpg']")));
        _ = Assert.Single(cut.FindAll("td.latest-loop video[src='/loop.mp4']"));
    }

    [Fact]
    public void NonCamera_ShowsNotApplicableForSnapshotAndLoop_AndItsReadableType()
    {
        SetEmptyLists();
        var chime = new Device { Id = Guid.NewGuid(), LocationId = _location.Id, ProviderDeviceId = "chime", Name = "Hall Chime", Type = "chime" };
        _ = DeviceRepositoryMock.Setup(m => m.GetByLocationIdAsync(_location.Id, It.IsAny<CancellationToken>())).ReturnsAsync([chime]);

        IRenderedComponent<AccountDevicesTab> cut = RenderDevices();

        cut.WaitForAssertion(() => Assert.Contains("Chime", cut.Find("#account-devices").TextContent));
        Assert.Equal(2, cut.FindAll("#account-devices .text-muted").Count(s => s.TextContent.Trim() == "N/A"));
        Assert.DoesNotContain("No snapshot yet", cut.Markup);
        Assert.DoesNotContain("No time lapse yet", cut.Markup);
    }

    [Fact]
    public void NoDevices_ShowsNoDevicesMessage()
    {
        SetEmptyLists();

        IRenderedComponent<AccountDevicesTab> cut = RenderDevices();

        cut.WaitForAssertion(() => Assert.Contains(Strings["DevicesNone"], cut.Markup));
    }
}
