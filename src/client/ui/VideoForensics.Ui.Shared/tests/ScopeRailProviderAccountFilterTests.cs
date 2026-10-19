namespace VideoForensics.Ui.Shared.Tests;

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Moq;
using Xunit;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Ui.Shared.Components.Scope;
using VideoForensics.Ui.Shared.Services;
using VideoForensics.Ui.Shared.Services.Cases;
using VideoForensics.Ui.Shared.Services.Scope;
using Device = VideoForensics.Data.Common.Entities.Device;

/// <summary>The rail's Provider and Account filters narrow the device list and scope the investigation to it.</summary>
public class ScopeRail_ProviderAccountFilter_Tests : BunitContext
{
    public ScopeRail_ProviderAccountFilter_Tests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddLocalization();
        Services.AddSingleton(TimeProvider.System);
        Services.AddScoped(_ => new ScopeState());

        var caseRepository = new Mock<ICaseRepository>();
        caseRepository
            .Setup(r => r.ListAsync(It.IsAny<CaseStatus?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ForensicCase>());
        Services.AddScoped(_ => caseRepository.Object);
        Services.AddScoped(sp => new CaseState(sp.GetRequiredService<ICaseRepository>(), sp.GetRequiredService<ScopeState>()));
        Services.AddScoped(sp => new PairedSessionState(sp.GetRequiredService<IJSRuntime>()));
    }

    private static Device MakeDevice(string name, Guid locationId) => new()
    {
        Id = Guid.NewGuid(),
        LocationId = locationId,
        ProviderDeviceId = Guid.NewGuid().ToString(),
        Name = name,
        Type = "camera",
    };

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

    private IReadOnlyList<string> VisibleDeviceIds(IRenderedComponent<ScopeRail> component) =>
        component.FindAll("input[data-device-id]").Select(i => i.GetAttribute("data-device-id")!).ToList();

    [Fact]
    public void ProviderPicked_ShowsOnlyThatProvidersDevices_AndScopesToThem()
    {
        var ringAccount = MakeAccount("Ring");
        var wyzeAccount = MakeAccount("Wyze");
        var ringLocation = MakeLocation();
        var wyzeLocation = MakeLocation();
        var ringDevice = MakeDevice("Ring Front", ringLocation.Id);
        var wyzeDevice = MakeDevice("Wyze Garage", wyzeLocation.Id);

        var deviceRepository = new Mock<IDeviceRepository>();
        deviceRepository
            .Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Device> { ringDevice, wyzeDevice });
        Services.AddScoped(_ => deviceRepository.Object);

        ScopeOwnershipTestDoubles.Register(
            Services,
            new[] { ringAccount, wyzeAccount },
            new Dictionary<Guid, IReadOnlyList<Location>>
            {
                [ringAccount.Id] = new[] { ringLocation },
                [wyzeAccount.Id] = new[] { wyzeLocation },
            });

        var component = Render<ScopeRail>();
        Assert.Equal(2, VisibleDeviceIds(component).Count);

        component.Find("[data-testid='scope-provider-filter']").Change("Ring");

        Assert.Equal(new[] { ringDevice.Id.ToString() }, VisibleDeviceIds(component));
        Assert.Equal(new[] { ringDevice.Id }, Services.GetRequiredService<ScopeState>().Current.DeviceIds);
    }

    [Fact]
    public void AccountPicked_ShowsOnlyThatAccountsDevices()
    {
        var ringAccount = MakeAccount("Ring");
        var otherRingAccount = MakeAccount("Ring");
        var ringLocation = MakeLocation();
        var otherLocation = MakeLocation();
        var ringDevice = MakeDevice("Ring Front", ringLocation.Id);
        var otherDevice = MakeDevice("Ring Back", otherLocation.Id);

        var deviceRepository = new Mock<IDeviceRepository>();
        deviceRepository
            .Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Device> { ringDevice, otherDevice });
        Services.AddScoped(_ => deviceRepository.Object);

        ScopeOwnershipTestDoubles.Register(
            Services,
            new[] { ringAccount, otherRingAccount },
            new Dictionary<Guid, IReadOnlyList<Location>>
            {
                [ringAccount.Id] = new[] { ringLocation },
                [otherRingAccount.Id] = new[] { otherLocation },
            });

        var component = Render<ScopeRail>();

        component.Find("[data-testid='scope-account-filter']").Change(otherRingAccount.Id.ToString());

        Assert.Equal(new[] { otherDevice.Id.ToString() }, VisibleDeviceIds(component));
    }
}
