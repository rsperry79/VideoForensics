using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using VideoForensics.Client.Ui.Shared.Components.Scope;
using VideoForensics.Client.Ui.Shared.Services.Cases;
using VideoForensics.Client.Ui.Shared.Services.Review;
using VideoForensics.Client.Ui.Shared.Services.Scope;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>
/// The scope rail on the Review pages shares its settings with Media through localStorage: it restores them on a bare
/// address and saves every change.
/// </summary>
public class ScopeRail_SharedSettings_Tests : ScopeRailTestContext
{
    public ScopeRail_SharedSettings_Tests()
    {
        _ = Services.AddScoped<ReviewSettingsStore>();
    }

    private void Saved(string json)
    {
        JSInterop.Setup<string?>("localStorage.getItem", ReviewSettingsStore.StorageKey).SetResult(json);
    }

    [Fact]
    public void ScopeRail_BareAddressWithSavedRange_RestoresTheRange()
    {
        // Arrange
        _ = RegisterDeviceRepository(MakeDevice("Front Door"));
        Saved("{\"query\":\"from=2026-09-01&to=2026-09-10\",\"eventId\":null}");

        // Act
        _ = Render<ScopeRail>();

        // Assert
        ScopeState state = Services.GetRequiredService<ScopeState>();
        Assert.Equal(new DateTime(2026, 9, 1), state.Current.FromUtc);
        Assert.Equal(new DateTime(2026, 9, 10), state.Current.ToUtc);
    }

    [Fact]
    public void ScopeRail_AddressCarriesAScope_IgnoresTheSavedSettings()
    {
        // Arrange
        _ = RegisterDeviceRepository(MakeDevice("Front Door"));
        Services.GetRequiredService<NavigationManager>().NavigateTo("/?from=2026-01-01&to=2026-02-01", replace: true);
        Saved("{\"query\":\"from=2026-09-01&to=2026-09-10\",\"eventId\":null}");

        // Act
        _ = Render<ScopeRail>();

        // Assert
        ScopeState state = Services.GetRequiredService<ScopeState>();
        Assert.Equal(new DateTime(2026, 1, 1), state.Current.FromUtc);
    }

    [Fact]
    public void ScopeRail_SavedDevicesCoverEveryCamera_ScopesToAllDevices()
    {
        // Arrange - Media saved the cameras it was showing; they are every camera, so the Review pages keep "all devices"
        Device front = MakeDevice("Front Door");
        Device back = MakeDevice("Back Door");
        _ = RegisterDeviceRepository(front, back);
        Saved($"{{\"query\":\"devices={front.Id:D},{back.Id:D}\",\"eventId\":null}}");

        // Act
        _ = Render<ScopeRail>();

        // Assert
        Assert.True(Services.GetRequiredService<ScopeState>().Current.IncludesAllDevices);
    }

    [Fact]
    public void ScopeRail_SavedDevicesAreOnlySomeCameras_ScopesToThose()
    {
        // Arrange
        Device front = MakeDevice("Front Door");
        Device back = MakeDevice("Back Door");
        _ = RegisterDeviceRepository(front, back);
        Saved($"{{\"query\":\"devices={front.Id:D}\",\"eventId\":null}}");

        // Act
        _ = Render<ScopeRail>();

        // Assert
        Assert.Equal(new[] { front.Id }, Services.GetRequiredService<ScopeState>().Current.DeviceIds);
    }

    [Fact]
    public void ScopeRail_SavedCase_ActivatesTheCase()
    {
        // Arrange
        _ = RegisterDeviceRepository();
        ForensicCase savedCase = MakeCase();
        Mock<ICaseRepository> caseRepository = RegisterCaseRepository(savedCase);
        _ = caseRepository.Setup(r => r.GetAsync(savedCase.Id, It.IsAny<CancellationToken>())).ReturnsAsync(savedCase);
        _ = caseRepository.Setup(r => r.GetDeviceIdsAsync(savedCase.Id, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        Saved($"{{\"query\":\"reviewcase={savedCase.Id:D}\",\"eventId\":null}}");

        // Act
        IRenderedComponent<ScopeRail> cut = Render<ScopeRail>();

        // Assert
        cut.WaitForAssertion(() => Assert.Equal(savedCase.Id, Services.GetRequiredService<CaseState>().ActiveCase?.Id));
    }

    [Fact]
    public void ScopeRail_RangeChange_SavesItToTheSharedKey()
    {
        // Arrange
        _ = RegisterFakeTime(new DateTime(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc));
        _ = RegisterDeviceRepository(MakeDevice("Front Door"));
        IRenderedComponent<ScopeRail> cut = Render<ScopeRail>();

        // Act
        cut.Find("[data-testid='scope-range']").Change("month");

        // Assert
        cut.WaitForAssertion(() => Assert.Contains(JSInterop.Invocations, i =>
            i.Identifier == "localStorage.setItem"
            && i.Arguments[0]!.ToString() == ReviewSettingsStore.StorageKey
            && i.Arguments[1]!.ToString()!.Contains("from=2026-08-25")));
    }
}
