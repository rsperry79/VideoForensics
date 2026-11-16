using VideoForensics.Client.Ui.Shared.Services.Scope;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Entities;

using Xunit;

using Device = VideoForensics.Core.Api.Contracts.DeviceDto;

namespace VideoForensics.Client.Ui.Shared.Tests.Services.Scope;

public class ScopeDeviceFilterTests
{
    private static ProviderAccountDto MakeAccount(string providerName)
    {
        return new(Guid.NewGuid(), Guid.NewGuid(), providerName, DateTime.UtcNow, null, true, null, null, null);
    }

    private static Location MakeLocation()
    {
        return new()
        {
            Id = Guid.NewGuid(),
            ProviderLocationId = Guid.NewGuid().ToString(),
            Name = "Home",
        };
    }

    private static Device MakeDevice(Guid locationId) => MakeEntity(locationId).ToDto();

    private static VideoForensics.Data.Common.Entities.Device MakeEntity(Guid locationId)
    {
        return new VideoForensics.Data.Common.Entities.Device()
        {
            Id = Guid.NewGuid(),
            LocationId = locationId,
            ProviderDeviceId = Guid.NewGuid().ToString(),
            Name = "Camera",
            Type = "camera",
        };
    }

    [Fact]
    public void BuildOwnership_DeviceAtAccountLocation_LinksDeviceToAccount()
    {
        ProviderAccountDto account = MakeAccount("Ring");
        Location location = MakeLocation();
        Device device = MakeDevice(location.Id);

        IReadOnlyList<DeviceOwnership> ownership = ScopeDeviceFilter.BuildOwnership(
            new[] { device },
            new[] { account },
            new Dictionary<Guid, IReadOnlyList<Guid>> { [account.Id] = new[] { location.Id } });

        DeviceOwnership row = Assert.Single(ownership);
        Assert.Equal(device.Id, row.DeviceId);
        Assert.Equal(account.Id, row.ProviderAccountId);
        Assert.Equal("Ring", row.ProviderName);
    }

    [Fact]
    public void BuildOwnership_LocationSharedByTwoAccounts_ProducesOneRowPerAccount()
    {
        ProviderAccountDto ring = MakeAccount("Ring");
        ProviderAccountDto wyze = MakeAccount("Wyze");
        Location location = MakeLocation();
        Device device = MakeDevice(location.Id);

        IReadOnlyList<DeviceOwnership> ownership = ScopeDeviceFilter.BuildOwnership(
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
        Location location = MakeLocation();
        Device device = MakeDevice(location.Id);

        // The account id is in the location map but not in the accounts list, so no row may be produced.
        IReadOnlyList<DeviceOwnership> ownership = ScopeDeviceFilter.BuildOwnership(
            new[] { device },
            Array.Empty<ProviderAccountDto>(),
            new Dictionary<Guid, IReadOnlyList<Guid>> { [Guid.NewGuid()] = new[] { location.Id } });

        Assert.Empty(ownership);
    }

    [Fact]
    public void Filter_NoFilters_ReturnsAllDevices()
    {
        Device[] devices = new[] { MakeDevice(Guid.NewGuid()), MakeDevice(Guid.NewGuid()) };

        IReadOnlyList<Device> visible = ScopeDeviceFilter.Filter(devices, Array.Empty<DeviceOwnership>(), null, null);

        Assert.Equal(devices.Select(d => d.Id), visible.Select(d => d.Id));
    }

    [Fact]
    public void Filter_ProviderSet_ReturnsOnlyDevicesOfThatProviderIgnoringCase()
    {
        ProviderAccountDto ring = MakeAccount("Ring");
        ProviderAccountDto wyze = MakeAccount("Wyze");
        Device ringDevice = MakeDevice(Guid.NewGuid());
        Device wyzeDevice = MakeDevice(Guid.NewGuid());
        DeviceOwnership[] ownership = new[]
        {
            new DeviceOwnership(ringDevice.Id, ring.Id, "Ring", Guid.Empty),
            new DeviceOwnership(wyzeDevice.Id, wyze.Id, "Wyze", Guid.Empty),
        };

        IReadOnlyList<Device> visible = ScopeDeviceFilter.Filter(new[] { ringDevice, wyzeDevice }, ownership, "ring", null);

        Assert.Equal(new[] { ringDevice.Id }, visible.Select(d => d.Id));
    }

    [Fact]
    public void Filter_AccountSet_ReturnsOnlyDevicesOfThatAccount()
    {
        ProviderAccountDto first = MakeAccount("Ring");
        ProviderAccountDto second = MakeAccount("Ring");
        Device firstDevice = MakeDevice(Guid.NewGuid());
        Device secondDevice = MakeDevice(Guid.NewGuid());
        DeviceOwnership[] ownership = new[]
        {
            new DeviceOwnership(firstDevice.Id, first.Id, "Ring", Guid.Empty),
            new DeviceOwnership(secondDevice.Id, second.Id, "Ring", Guid.Empty),
        };

        IReadOnlyList<Device> visible = ScopeDeviceFilter.Filter(new[] { firstDevice, secondDevice }, ownership, null, second.Id);

        Assert.Equal(new[] { secondDevice.Id }, visible.Select(d => d.Id));
    }

    [Fact]
    public void Filter_FilterSetAndDeviceHasNoOwnership_ExcludesDevice()
    {
        Device orphan = MakeDevice(Guid.NewGuid());

        IReadOnlyList<Device> visible = ScopeDeviceFilter.Filter(new[] { orphan }, Array.Empty<DeviceOwnership>(), "Ring", null);

        Assert.Empty(visible);
    }

    [Fact]
    public void AccountsForProvider_NoProvider_ReturnsAllAccounts()
    {
        ProviderAccountDto[] accounts = new[] { MakeAccount("Ring"), MakeAccount("Wyze") };

        IReadOnlyList<ProviderAccountDto> options = ScopeDeviceFilter.AccountsForProvider(accounts, null);

        Assert.Equal(2, options.Count);
    }

    [Fact]
    public void AccountsForProvider_ProviderSet_ReturnsOnlyThatProvidersAccounts()
    {
        ProviderAccountDto ring = MakeAccount("Ring");
        ProviderAccountDto[] accounts = new[] { ring, MakeAccount("Wyze") };

        IReadOnlyList<ProviderAccountDto> options = ScopeDeviceFilter.AccountsForProvider(accounts, "Ring");

        Assert.Equal(new[] { ring.Id }, options.Select(a => a.Id));
    }
}
