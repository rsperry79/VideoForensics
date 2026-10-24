
using Moq;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Client.Ui.Shared.Services.Scope;

using Xunit;

using Device = VideoForensics.Data.Common.Entities.Device;

namespace VideoForensics.Client.Ui.Shared.Tests.Services.Scope;

public class DeviceOwnershipServiceTests
{
    private readonly Mock<IProviderAccountRepository> _accounts = new();
    private readonly Mock<ILocationRepository> _locations = new();
    private readonly Mock<IUserRepository> _users = new();

    private DeviceOwnershipService CreateService()
    {
        return new(_accounts.Object, _locations.Object, _users.Object);
    }

    private static ProviderAccount MakeAccount(string providerName, Guid userId)
    {
        return new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ProviderName = providerName,
            LinkedUtc = DateTime.UtcNow,
        };
    }

    private static Device MakeDevice(Guid locationId)
    {
        return new()
        {
            Id = Guid.NewGuid(),
            LocationId = locationId,
            ProviderDeviceId = Guid.NewGuid().ToString(),
            Name = "Camera",
            Type = "camera",
        };
    }

    [Fact]
    public async Task LoadAsync_AccountsAndLocations_BuildsOwnershipAndSortedProviderNames()
    {
        ProviderAccount ring = MakeAccount("Ring", Guid.NewGuid());
        ProviderAccount wyze = MakeAccount("Wyze", Guid.NewGuid());
        var location = new Location { Id = Guid.NewGuid(), ProviderLocationId = "L1", Name = "Home" };
        Device device = MakeDevice(location.Id);

        _ = _accounts.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[] { wyze, ring });
        _ = _locations.Setup(r => r.ListByProviderAccountIdAsync(ring.Id, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { location });
        _ = _locations.Setup(r => r.ListByProviderAccountIdAsync(wyze.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<Location>());
        _ = _users.Setup(r => r.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((User?)null);

        DeviceOwnershipSnapshot snapshot = await CreateService().LoadAsync(new[] { device }, CancellationToken.None);

        Assert.Equal(new[] { "Ring", "Wyze" }, snapshot.ProviderNames);
        DeviceOwnership row = Assert.Single(snapshot.Ownership);
        Assert.Equal(device.Id, row.DeviceId);
        Assert.Equal(ring.Id, row.ProviderAccountId);
    }

    [Fact]
    public async Task LoadAsync_UserLookupThrows_FallsBackToProviderName()
    {
        ProviderAccount ring = MakeAccount("Ring", Guid.NewGuid());
        _ = _accounts.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[] { ring });
        _ = _locations.Setup(r => r.ListByProviderAccountIdAsync(ring.Id, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<Location>());
        _ = _users.Setup(r => r.GetAsync(ring.UserId, It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("db down"));

        DeviceOwnershipSnapshot snapshot = await CreateService().LoadAsync(Array.Empty<Device>(), CancellationToken.None);

        Assert.Equal("Ring", snapshot.AccountNames[ring.Id]);
    }

    [Fact]
    public async Task LoadAsync_AccountRepositoryThrows_ReturnsEmptySnapshot()
    {
        _ = _accounts.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("db down"));

        DeviceOwnershipSnapshot snapshot = await CreateService().LoadAsync(new[] { MakeDevice(Guid.NewGuid()) }, CancellationToken.None);

        Assert.Empty(snapshot.Accounts);
        Assert.Empty(snapshot.Ownership);
        Assert.Empty(snapshot.ProviderNames);
    }
}
