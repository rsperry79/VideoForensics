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

/// <summary>An account whose filter matches no device must mark the scope as no-match, not as all devices.</summary>
public class ScopeRail_NoMatch_Tests : BunitContext
{
    public ScopeRail_NoMatch_Tests()
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

    [Fact]
    public void AccountWithNoDevices_MarksScopeAsNoMatch_AndHidesDevices()
    {
        var idleAccount = new ProviderAccount { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), ProviderName = "Ring", LinkedUtc = DateTime.UtcNow };
        var otherLocation = new Location { Id = Guid.NewGuid(), ProviderLocationId = "O", Name = "Other" };
        var device = new Device { Id = Guid.NewGuid(), LocationId = otherLocation.Id, ProviderDeviceId = "d", Name = "Camera", Type = "camera" };

        var deviceRepository = new Mock<IDeviceRepository>();
        deviceRepository
            .Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Device> { device });
        Services.AddScoped(_ => deviceRepository.Object);

        // The account has no locations, so none of the devices belong to it.
        ScopeOwnershipTestDoubles.Register(
            Services,
            new[] { idleAccount },
            new Dictionary<Guid, IReadOnlyList<Location>> { [idleAccount.Id] = Array.Empty<Location>() });

        var component = Render<ScopeRail>();
        component.Find("[data-testid='scope-account-filter']").Change(idleAccount.Id.ToString());

        var scope = Services.GetRequiredService<ScopeState>().Current;
        Assert.True(scope.MatchesNoDevices);
        Assert.False(scope.IncludesAllDevices);
        Assert.Empty(component.FindAll("input[data-device-id]"));
    }
}
