namespace VideoForensics.Ui.Shared.Tests;

using VideoForensics.Data.Common.Entities;
using Xunit;
using VideoForensics.Ui.Shared.Services.Scope;
using Device = VideoForensics.Data.Common.Entities.Device;

public class ScopeDeviceFilterTests
{
    private static ProviderAccount MakeAccount(string providerName) => new()
    {
        Id = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        ProviderName = providerName,
        LinkedUtc = DateTime.UtcNow,
    };

    private static Location MakeLocation() => new()
    {
        Id = Guid.NewGuid(),
        ProviderLocationId = Guid.NewGuid().ToString(),
        Name = "Home",
    };

    private static Device MakeDevice(Guid locationId) => new()
    {
        Id = Guid.NewGuid(),
        LocationId = locationId,
        ProviderDeviceId = Guid.NewGuid().ToString(),
        Name = "Camera",
        Type = "camera",
    };

    [Fact]
    public void BuildOwnership_DeviceAtAccountLocation_LinksDeviceToAccount()
    {
        var account = MakeAccount("Ring");
        var location = MakeLocation();
        var device = MakeDevice(location.Id);

        var ownership = ScopeDeviceFilter.BuildOwnership(
            new[] { device },
            new[] { account },
            new Dictionary<Guid, IReadOnlyList<Guid>> { [account.Id] = new[] { location.Id } });

        var row = Assert.Single(ownership);
        Assert.Equal(device.Id, row.DeviceId);
        Assert.Equal(account.Id, row.ProviderAccountId);
        Assert.Equal("Ring", row.ProviderName);
    }

    [Fact]
    public void BuildOwnership_LocationSharedByTwoAccounts_ProducesOneRowPerAccount()
    {
        var ring = MakeAccount("Ring");
        var wyze = MakeAccount("Wyze");
        var location = MakeLocation();
        var device = MakeDevice(location.Id);

        var ownership = ScopeDeviceFilter.BuildOwnership(
            new[] { device },
            new[] { ring, wyze },
            new Dictionary<Guid, IReadOnlyList<Guid>>
            {
                [ring.Id] = new[] { location.Id },
                [wyze.Id] = new[] { location.Id },
            });

        Assert.Equal(2, ownership.Count);
        Assert.Contains(ownership, o => o.ProviderAccountId == ring.Id && o.ProviderName == "Ring");
        Assert.Contains(ownership, o => o.ProviderAccountId == wyze.Id && o.ProviderName == "Wyze");
    }

    [Fact]
    public void BuildOwnership_LocationOfUnknownAccount_IsIgnored()
    {
        var location = MakeLocation();
        var device = MakeDevice(location.Id);

        // The account id is in the location map but not in the accounts list, so no row may be produced.
        var ownership = ScopeDeviceFilter.BuildOwnership(
            new[] { device },
            Array.Empty<ProviderAccount>(),
            new Dictionary<Guid, IReadOnlyList<Guid>> { [Guid.NewGuid()] = new[] { location.Id } });

        Assert.Empty(ownership);
    }

    [Fact]
    public void Filter_NoFilters_ReturnsAllDevices()
    {
        var devices = new[] { MakeDevice(Guid.NewGuid()), MakeDevice(Guid.NewGuid()) };

        var visible = ScopeDeviceFilter.Filter(devices, Array.Empty<DeviceOwnership>(), null, null);

        Assert.Equal(devices.Select(d => d.Id), visible.Select(d => d.Id));
    }

    [Fact]
    public void Filter_ProviderSet_ReturnsOnlyDevicesOfThatProviderIgnoringCase()
    {
        var ring = MakeAccount("Ring");
        var wyze = MakeAccount("Wyze");
        var ringDevice = MakeDevice(Guid.NewGuid());
        var wyzeDevice = MakeDevice(Guid.NewGuid());
        var ownership = new[]
        {
            new DeviceOwnership(ringDevice.Id, ring.Id, "Ring"),
            new DeviceOwnership(wyzeDevice.Id, wyze.Id, "Wyze"),
        };

        var visible = ScopeDeviceFilter.Filter(new[] { ringDevice, wyzeDevice }, ownership, "ring", null);

        Assert.Equal(new[] { ringDevice.Id }, visible.Select(d => d.Id));
    }

    [Fact]
    public void Filter_AccountSet_ReturnsOnlyDevicesOfThatAccount()
    {
        var first = MakeAccount("Ring");
        var second = MakeAccount("Ring");
        var firstDevice = MakeDevice(Guid.NewGuid());
        var secondDevice = MakeDevice(Guid.NewGuid());
        var ownership = new[]
        {
            new DeviceOwnership(firstDevice.Id, first.Id, "Ring"),
            new DeviceOwnership(secondDevice.Id, second.Id, "Ring"),
        };

        var visible = ScopeDeviceFilter.Filter(new[] { firstDevice, secondDevice }, ownership, null, second.Id);

        Assert.Equal(new[] { secondDevice.Id }, visible.Select(d => d.Id));
    }

    [Fact]
    public void Filter_FilterSetAndDeviceHasNoOwnership_ExcludesDevice()
    {
        var orphan = MakeDevice(Guid.NewGuid());

        var visible = ScopeDeviceFilter.Filter(new[] { orphan }, Array.Empty<DeviceOwnership>(), "Ring", null);

        Assert.Empty(visible);
    }

    [Fact]
    public void AccountsForProvider_NoProvider_ReturnsAllAccounts()
    {
        var accounts = new[] { MakeAccount("Ring"), MakeAccount("Wyze") };

        var options = ScopeDeviceFilter.AccountsForProvider(accounts, null);

        Assert.Equal(2, options.Count);
    }

    [Fact]
    public void AccountsForProvider_ProviderSet_ReturnsOnlyThatProvidersAccounts()
    {
        var ring = MakeAccount("Ring");
        var accounts = new[] { ring, MakeAccount("Wyze") };

        var options = ScopeDeviceFilter.AccountsForProvider(accounts, "Ring");

        Assert.Equal(new[] { ring.Id }, options.Select(a => a.Id));
    }
}
