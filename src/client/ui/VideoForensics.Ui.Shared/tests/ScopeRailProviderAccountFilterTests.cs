
using Bunit;

using Microsoft.Extensions.DependencyInjection;

using Moq;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Client.Ui.Shared.Components.Scope;
using VideoForensics.Client.Ui.Shared.Services.Scope;

using Xunit;

using Device = VideoForensics.Data.Common.Entities.Device;

namespace VideoForensics.Client.Ui.Shared.Tests;
/// <summary>The rail's Provider and Account filters narrow the device list and scope the investigation to it.</summary>
public class ScopeRail_ProviderAccountFilter_Tests : ScopeRailTestContext
{
    public ScopeRail_ProviderAccountFilter_Tests()
    {
        _ = Services.AddScoped(_ => new ScopeState());

        // Overrides the base's Open-only case repository: this rail test also queries with a nullable status.
        var caseRepository = new Mock<ICaseRepository>();
        _ = caseRepository
            .Setup(r => r.ListAsync(It.IsAny<CaseStatus?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _ = Services.AddScoped(_ => caseRepository.Object);
    }

    private static Device MakeDevice(string name, Guid locationId)
    {
        return new()
        {
            Id = Guid.NewGuid(),
            LocationId = locationId,
            ProviderDeviceId = Guid.NewGuid().ToString(),
            Name = name,
            Type = "camera",
        };
    }

    private static ProviderAccount MakeAccount(string providerName)
    {
        return new()
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            ProviderName = providerName,
            LinkedUtc = DateTime.UtcNow,
        };
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

    private IReadOnlyList<string> VisibleDeviceIds(IRenderedComponent<ScopeRail> component)
    {
        // The device list opens from its summary button; open it once so the checkboxes can be read.
        if (component.FindAll("[data-testid='scope-device-list']").Count == 0 && component.FindAll("[data-testid='scope-device-button']").Count > 0)
        {
            component.Find("[data-testid='scope-device-button']").Click();
        }

        return component.FindAll("input[data-device-id]").Select(i => i.GetAttribute("data-device-id")!).ToList();
    }

    [Fact]
    public void ProviderPicked_ShowsOnlyThatProvidersDevices_AndScopesToThem()
    {
        ProviderAccount ringAccount = MakeAccount("Ring");
        ProviderAccount wyzeAccount = MakeAccount("Wyze");
        Location ringLocation = MakeLocation();
        Location wyzeLocation = MakeLocation();
        Device ringDevice = MakeDevice("Ring Front", ringLocation.Id);
        Device wyzeDevice = MakeDevice("Wyze Garage", wyzeLocation.Id);

        var deviceRepository = new Mock<IDeviceRepository>();
        _ = deviceRepository
            .Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([ringDevice, wyzeDevice]);
        _ = Services.AddScoped(_ => deviceRepository.Object);

        ScopeOwnershipTestDoubles.Register(
            Services,
            new[] { ringAccount, wyzeAccount },
            new Dictionary<Guid, IReadOnlyList<Location>>
            {
                [ringAccount.Id] = new[] { ringLocation },
                [wyzeAccount.Id] = new[] { wyzeLocation },
            });

        IRenderedComponent<ScopeRail> component = Render<ScopeRail>();
        Assert.Equal(2, VisibleDeviceIds(component).Count);

        component.Find("[data-testid='scope-provider-filter']").Change("Ring");

        Assert.Equal(new[] { ringDevice.Id.ToString() }, VisibleDeviceIds(component));
        Assert.Equal(new[] { ringDevice.Id }, Services.GetRequiredService<ScopeState>().Current.DeviceIds);
    }

    [Fact]
    public void AccountPicked_ShowsOnlyThatAccountsDevices()
    {
        ProviderAccount ringAccount = MakeAccount("Ring");
        ProviderAccount otherRingAccount = MakeAccount("Ring");
        Location ringLocation = MakeLocation();
        Location otherLocation = MakeLocation();
        Device ringDevice = MakeDevice("Ring Front", ringLocation.Id);
        Device otherDevice = MakeDevice("Ring Back", otherLocation.Id);

        var deviceRepository = new Mock<IDeviceRepository>();
        _ = deviceRepository
            .Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([ringDevice, otherDevice]);
        _ = Services.AddScoped(_ => deviceRepository.Object);

        ScopeOwnershipTestDoubles.Register(
            Services,
            new[] { ringAccount, otherRingAccount },
            new Dictionary<Guid, IReadOnlyList<Location>>
            {
                [ringAccount.Id] = new[] { ringLocation },
                [otherRingAccount.Id] = new[] { otherLocation },
            });

        IRenderedComponent<ScopeRail> component = Render<ScopeRail>();

        component.Find("[data-testid='scope-account-filter']").Change(otherRingAccount.Id.ToString());

        Assert.Equal(new[] { otherDevice.Id.ToString() }, VisibleDeviceIds(component));
    }

    [Fact]
    public void LocationPicked_ShowsOnlyThatLocationsDevices_AndScopesToThem()
    {
        ProviderAccount ringAccount = MakeAccount("Ring");
        Location porch = MakeLocation();
        Location garage = MakeLocation();
        porch.Name = "Porch";
        garage.Name = "Garage";
        Device porchDevice = MakeDevice("Ring Front", porch.Id);
        Device garageDevice = MakeDevice("Ring Garage", garage.Id);

        var deviceRepository = new Mock<IDeviceRepository>();
        _ = deviceRepository
            .Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([porchDevice, garageDevice]);
        _ = Services.AddScoped(_ => deviceRepository.Object);

        ScopeOwnershipTestDoubles.Register(
            Services,
            new[] { ringAccount },
            new Dictionary<Guid, IReadOnlyList<Location>>
            {
                [ringAccount.Id] = new[] { porch, garage },
            });

        IRenderedComponent<ScopeRail> component = Render<ScopeRail>();
        Assert.Equal(3, component.FindAll("[data-testid='scope-location-filter'] option").Count);
        Assert.Equal(2, VisibleDeviceIds(component).Count);

        component.Find("[data-testid='scope-location-filter']").Change(garage.Id.ToString());

        Assert.Equal(new[] { garageDevice.Id.ToString() }, VisibleDeviceIds(component));
        Assert.Equal(new[] { garageDevice.Id }, Services.GetRequiredService<ScopeState>().Current.DeviceIds);
    }
}
