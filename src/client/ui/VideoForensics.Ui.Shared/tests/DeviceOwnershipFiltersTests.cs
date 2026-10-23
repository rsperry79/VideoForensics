
using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Ui.Shared.Components.Scope;
using VideoForensics.Ui.Shared.Services.Scope;

using Xunit;

using Device = VideoForensics.Data.Common.Entities.Device;

namespace VideoForensics.Ui.Shared.Tests;
/// <summary>The shared Provider/Account filter reports which devices remain visible after each change.</summary>
public class DeviceOwnershipFilters_Tests : BunitContext
{
    public DeviceOwnershipFilters_Tests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _ = Services.AddLocalization();
    }

    [Fact]
    public async Task ProviderPicked_ReportsOnlyThatProvidersDevices()
    {
        var ring = new ProviderAccount { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), ProviderName = "Ring", LinkedUtc = DateTime.UtcNow };
        var wyze = new ProviderAccount { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), ProviderName = "Wyze", LinkedUtc = DateTime.UtcNow };
        var ringLocation = new Location { Id = Guid.NewGuid(), ProviderLocationId = "R", Name = "R" };
        var wyzeLocation = new Location { Id = Guid.NewGuid(), ProviderLocationId = "W", Name = "W" };
        var ringDevice = new Device { Id = Guid.NewGuid(), LocationId = ringLocation.Id, ProviderDeviceId = "r", Name = "Ring Front", Type = "camera" };
        var wyzeDevice = new Device { Id = Guid.NewGuid(), LocationId = wyzeLocation.Id, ProviderDeviceId = "w", Name = "Wyze Garage", Type = "camera" };

        ScopeOwnershipTestDoubles.Register(
            Services,
            new[] { ring, wyze },
            new Dictionary<Guid, IReadOnlyList<Location>>
            {
                [ring.Id] = new[] { ringLocation },
                [wyze.Id] = new[] { wyzeLocation },
            });

        DeviceFilterResult? reported = null;
        IRenderedComponent<DeviceOwnershipFilters> component = Render<DeviceOwnershipFilters>(parameters => parameters
            .Add(p => p.Devices, [ringDevice, wyzeDevice])
            .Add(p => p.VisibleDevicesChanged, EventCallback.Factory.Create<DeviceFilterResult>(this, r => reported = r)));

        component.Find("[data-testid='provider-filter']").Change("Ring");
        await component.InvokeAsync(() => { });

        Assert.NotNull(reported);
        Assert.True(reported!.IsFiltered);
        Assert.Equal(new[] { ringDevice.Id }, reported.Devices.Select(d => d.Id));
    }
}
