
using Bunit;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

using Moq;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Client.Ui.Shared.Components.Scope;
using VideoForensics.Client.Ui.Shared.Services;
using VideoForensics.Client.Ui.Shared.Services.Cases;
using VideoForensics.Client.Ui.Shared.Services.Scope;

using Xunit;

using Device = VideoForensics.Data.Common.Entities.Device;

namespace VideoForensics.Client.Ui.Shared.Tests;
/// <summary>An account whose filter matches no device must mark the scope as no-match, not as all devices.</summary>
public class ScopeRail_NoMatch_Tests : BunitContext
{
    public ScopeRail_NoMatch_Tests()
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
    public void AccountWithNoDevices_MarksScopeAsNoMatch_AndHidesDevices()
    {
        var idleAccount = new ProviderAccount { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), ProviderName = "Ring", LinkedUtc = DateTime.UtcNow };
        var otherLocation = new Location { Id = Guid.NewGuid(), ProviderLocationId = "O", Name = "Other" };
        var device = new Device { Id = Guid.NewGuid(), LocationId = otherLocation.Id, ProviderDeviceId = "d", Name = "Camera", Type = "camera" };

        var deviceRepository = new Mock<IDeviceRepository>();
        _ = deviceRepository
            .Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([device]);
        _ = Services.AddScoped(_ => deviceRepository.Object);

        // The account has no locations, so none of the devices belong to it.
        ScopeOwnershipTestDoubles.Register(
            Services,
            new[] { idleAccount },
            new Dictionary<Guid, IReadOnlyList<Location>> { [idleAccount.Id] = Array.Empty<Location>() });

        IRenderedComponent<ScopeRail> component = Render<ScopeRail>();
        component.Find("[data-testid='scope-account-filter']").Change(idleAccount.Id.ToString());

        ForensicScope scope = Services.GetRequiredService<ScopeState>().Current;
        Assert.True(scope.MatchesNoDevices);
        Assert.False(scope.IncludesAllDevices);
        Assert.Empty(component.FindAll("input[data-device-id]"));
    }
}
