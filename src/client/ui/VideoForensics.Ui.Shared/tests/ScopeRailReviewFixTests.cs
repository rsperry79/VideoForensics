
using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

using Moq;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Ui.Shared.Components.Scope;
using VideoForensics.Ui.Shared.Services;
using VideoForensics.Ui.Shared.Services.Cases;
using VideoForensics.Ui.Shared.Services.Scope;

using Xunit;

using Device = VideoForensics.Data.Common.Entities.Device;

namespace VideoForensics.Ui.Shared.Tests;
/// <summary>Rail fixes: unchecking the last device under a filter means no device; an orphaned no-match flag is dropped on load.</summary>
public class ScopeRail_ReviewFixes_Tests : BunitContext
{
    public ScopeRail_ReviewFixes_Tests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _ = Services.AddLocalization();
        _ = Services.AddSingleton(TimeProvider.System);
        _ = Services.AddScoped(_ => new ScopeState());

        var caseRepository = new Mock<ICaseRepository>();
        _ = caseRepository
            .Setup(r => r.ListAsync(It.IsAny<CaseStatus?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _ = Services.AddScoped(_ => caseRepository.Object);
        _ = Services.AddScoped(sp => new CaseState(sp.GetRequiredService<ICaseRepository>(), sp.GetRequiredService<ScopeState>()));
        _ = Services.AddScoped(sp => new PairedSessionState(sp.GetRequiredService<IJSRuntime>()));
    }

    [Fact]
    public async Task UncheckingLastVisibleDeviceUnderFilter_MarksNoMatch_NotAllDevices()
    {
        var account = new ProviderAccount { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), ProviderName = "Ring", LinkedUtc = DateTime.UtcNow };
        var location = new Location { Id = Guid.NewGuid(), ProviderLocationId = "L", Name = "Home" };
        var device = new Device { Id = Guid.NewGuid(), LocationId = location.Id, ProviderDeviceId = "d", Name = "Front", Type = "camera" };

        var deviceRepository = new Mock<IDeviceRepository>();
        _ = deviceRepository.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([device]);
        _ = Services.AddScoped(_ => deviceRepository.Object);
        ScopeOwnershipTestDoubles.Register(
            Services,
            new[] { account },
            new Dictionary<Guid, IReadOnlyList<Location>> { [account.Id] = new[] { location } });

        IRenderedComponent<ScopeRail> component = Render<ScopeRail>();
        component.Find("[data-testid='scope-provider-filter']").Change("Ring");
        component.Find($"input[data-device-id='{device.Id:D}']").Change(false);

        ForensicScope scope = Services.GetRequiredService<ScopeState>().Current;
        Assert.True(scope.MatchesNoDevices);
        Assert.False(scope.IncludesAllDevices);
    }

    [Fact]
    public void OrphanedNoMatchFlagInUrl_IsDroppedOnLoad()
    {
        // Register everything before the first service is resolved: bUnit rejects later registrations.
        var deviceRepository = new Mock<IDeviceRepository>();
        _ = deviceRepository.Setup(r => r.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        _ = Services.AddScoped(_ => deviceRepository.Object);
        ScopeOwnershipTestDoubles.RegisterEmpty(Services);

        Services.GetRequiredService<NavigationManager>().NavigateTo("/analyze?devices=none", replace: true);

        _ = Render<ScopeRail>();

        ForensicScope scope = Services.GetRequiredService<ScopeState>().Current;
        Assert.False(scope.MatchesNoDevices);
        Assert.True(scope.IncludesAllDevices);
    }
}
