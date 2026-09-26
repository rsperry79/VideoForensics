namespace VideoForensics.Ui.Shared.Tests;

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using Moq;
using Xunit;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Ui.Shared.Layout.Mobile;
using VideoForensics.Ui.Shared.Resources;
using VideoForensics.Ui.Shared.Services;
using VideoForensics.Ui.Shared.Services.Cases;
using VideoForensics.Ui.Shared.Services.Inspector;
using VideoForensics.Ui.Shared.Services.Scope;

/// <summary>
/// Tests for MobileFiltersSheet component.
/// </summary>
public class MobileFiltersSheet_Rendering_Tests : BunitContext
{
    public MobileFiltersSheet_Rendering_Tests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        // Register Syncfusion services required by SfButton
        Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();

        // Register services needed by MobileFiltersSheet (some tests override ScopeState)
        // Don't add ScopeState here - let each test register it as needed
        Services.AddScoped(sp => new CaseState(sp.GetRequiredService<ICaseRepository>(), sp.GetRequiredService<ScopeState>()));

        // Mock repositories
        var caseRepoMock = new Mock<ICaseRepository>();
        caseRepoMock
            .Setup(r => r.ListAsync(CaseStatus.Open, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ForensicCase>());
        Services.AddScoped(_ => caseRepoMock.Object);

        var deviceRepoMock = new Mock<IDeviceRepository>();
        deviceRepoMock
            .Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Device>());
        Services.AddScoped(_ => deviceRepoMock.Object);

        Services.AddScoped(sp => new PairedSessionState(sp.GetRequiredService<IJSRuntime>()));

        // Register localizer
        var localizerMock = new Mock<IStringLocalizer<SharedResources>>();
        localizerMock
            .Setup(l => l[It.IsAny<string>()])
            .Returns((string key) => new LocalizedString(key, key));
        Services.AddScoped(_ => localizerMock.Object);

        Services.AddSingleton(TimeProvider.System);
    }

    private static Device MakeDevice(string name) => new()
    {
        Id = Guid.NewGuid(),
        LocationId = Guid.NewGuid(),
        ProviderDeviceId = Guid.NewGuid().ToString(),
        Name = name,
        Type = "camera",
    };

    private Mock<IDeviceRepository> RegisterDeviceRepository(params Device[] devices)
    {
        var deviceRepository = new Mock<IDeviceRepository>();
        deviceRepository
            .Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(devices.ToList());
        Services.AddScoped(_ => deviceRepository.Object);
        return deviceRepository;
    }

    private FakeTimeProvider RegisterFakeTime(DateTime fixedUtcNow)
    {
        var timeProvider = new FakeTimeProvider(fixedUtcNow);
        Services.AddSingleton<TimeProvider>(timeProvider);
        Services.AddScoped(_ => new ScopeState(timeProvider));
        return timeProvider;
    }

    [Fact]
    public void FiltersButton_IsPresent()
    {
        // Arrange
        Services.AddScoped(sp => new ScopeState());  // Register default ScopeState
        RegisterDeviceRepository();

        // Act
        var component = Render<MobileFiltersSheet>();

        // Assert
        var filtersButton = component.FindAll("[data-testid='mobile-filters-toggle']");
        Assert.NotEmpty(filtersButton);
    }

    [Fact]
    public void FiltersButton_TogglesSheet()
    {
        // Arrange
        Services.AddScoped(sp => new ScopeState());  // Register default ScopeState
        RegisterDeviceRepository();

        var component = Render<MobileFiltersSheet>();

        // Act - click filters button to open sheet
        var filtersButton = component.Find("[data-testid='mobile-filters-toggle']");
        filtersButton.Click();

        // Assert - sheet is visible and contains ScopeRail markup
        var sheet = component.FindAll("[data-testid='mobile-filters-sheet']");
        Assert.NotEmpty(sheet);

        // Verify ScopeRail content is rendered (e.g., case picker or date inputs)
        Assert.True(component.FindAll("[data-testid='case-picker']").Count > 0 ||
                   component.FindAll("input[type='date']").Count > 0,
                   "ScopeRail markup should be present in filters sheet");
    }

    [Fact]
    public void FiltersSheet_HasCloseButton()
    {
        // Arrange
        Services.AddScoped(sp => new ScopeState());  // Register default ScopeState
        RegisterDeviceRepository();

        var component = Render<MobileFiltersSheet>();
        var filtersButton = component.Find("[data-testid='mobile-filters-toggle']");
        filtersButton.Click();

        // Act - click close button
        var closeButton = component.Find("[data-testid='mobile-filters-close']");
        closeButton.Click();

        // Assert - sheet is closed
        Assert.Empty(component.FindAll("[data-testid='mobile-filters-sheet']"));
    }

    [Fact]
    public void FiltersSheetBackdrop_ClosesSheet()
    {
        // Arrange
        Services.AddScoped(sp => new ScopeState());  // Register default ScopeState
        RegisterDeviceRepository();

        var component = Render<MobileFiltersSheet>();
        var filtersButton = component.Find("[data-testid='mobile-filters-toggle']");
        filtersButton.Click();

        // Act - click backdrop
        var backdrop = component.Find("[data-testid='mobile-filters-backdrop']");
        backdrop.Click();

        // Assert - sheet is closed
        Assert.Empty(component.FindAll("[data-testid='mobile-filters-sheet']"));
    }

    [Fact]
    public void FiltersIndicator_DoesNotAppearWithDefaultScope()
    {
        // Arrange
        var fixedNow = new DateTime(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);
        RegisterFakeTime(fixedNow);
        RegisterDeviceRepository();

        // Act
        var component = Render<MobileFiltersSheet>();

        // Assert - indicator should not be present with default scope
        Assert.Empty(component.FindAll("[data-testid='mobile-filters-indicator']"));
    }

    [Fact]
    public async Task FiltersIndicator_AppearsWhenDeviceSelected()
    {
        // Arrange
        var fixedNow = new DateTime(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);
        var timeProvider = RegisterFakeTime(fixedNow);
        var device = MakeDevice("Test Camera");
        RegisterDeviceRepository(device);

        var scopeState = new ScopeState(timeProvider);
        Services.AddScoped(_ => scopeState);

        var component = Render<MobileFiltersSheet>();

        // Act - update scope to include a device
        var customScope = new ForensicScope(
            DeviceIds: new List<Guid> { device.Id },
            FromUtc: fixedNow.Date.AddDays(-7),
            ToUtc: fixedNow.Date.AddDays(1),
            Search: null);

        await component.InvokeAsync(() =>
        {
            scopeState.Set(customScope);
        });

        // Assert - indicator should now be present
        Assert.NotEmpty(component.FindAll("[data-testid='mobile-filters-indicator']"));
    }

}

/// <summary>
/// Tests for MobileInspectorDrawer component.
/// </summary>
public class MobileInspectorDrawer_Rendering_Tests : BunitContext
{
    public MobileInspectorDrawer_Rendering_Tests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        // Register Syncfusion services required by InspectorPanel components
        Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();

        // Register services needed by MobileInspectorDrawer
        Services.AddScoped(sp => new InspectorState());
        Services.AddScoped(sp => new CaseState(sp.GetRequiredService<ICaseRepository>(), sp.GetRequiredService<ScopeState>()));
        Services.AddScoped(sp => new PairedSessionState(sp.GetRequiredService<IJSRuntime>()));
        Services.AddScoped(sp => new ScopeState());

        // Mock repositories
        var caseRepoMock = new Mock<ICaseRepository>();
        Services.AddScoped(_ => caseRepoMock.Object);

        var localizerMock = new Mock<IStringLocalizer<SharedResources>>();
        localizerMock
            .Setup(l => l[It.IsAny<string>()])
            .Returns((string key) => new LocalizedString(key, key));
        Services.AddScoped(_ => localizerMock.Object);

        Services.AddSingleton(TimeProvider.System);
    }

    [Fact]
    public void InspectorClosed_WhenInspectorStateNull()
    {
        // Arrange
        var inspectorState = new InspectorState();
        Services.AddScoped(_ => inspectorState);

        var component = Render<MobileInspectorDrawer>();

        // Act / Assert - inspector drawer should not be visible
        Assert.Empty(component.FindAll("[data-testid='mobile-inspector']"));
    }

    [Fact]
    public async Task InspectorDrawer_OpensWhenStateUpdated()
    {
        // Arrange
        var inspectorState = new InspectorState();
        Services.AddScoped(_ => inspectorState);

        var component = Render<MobileInspectorDrawer>();
        var model = new InspectorModel(Title: "Test Item", Fields: new { Id = 1 });

        // Act
        await component.InvokeAsync(() =>
        {
            inspectorState.Show(model);
        });

        // Assert - inspector drawer should appear with the model
        var drawer = component.FindAll("[data-testid='mobile-inspector']");
        Assert.NotEmpty(drawer);
        Assert.Contains("Test Item", component.Markup);
    }

    [Fact]
    public void InspectorCloseButton_ClearsState()
    {
        // Arrange
        var inspectorState = new InspectorState();
        Services.AddScoped(_ => inspectorState);

        var component = Render<MobileInspectorDrawer>();
        var model = new InspectorModel(Title: "Test Item", Fields: new { });
        inspectorState.Show(model);

        // Verify drawer is open
        Assert.NotEmpty(component.FindAll("[data-testid='mobile-inspector']"));

        // Act - click close button
        var closeButton = component.Find("[data-testid='inspector-close']");
        closeButton.Click();

        // Assert - drawer is closed and state is cleared
        Assert.Empty(component.FindAll("[data-testid='mobile-inspector']"));
        Assert.Null(inspectorState.Current);
    }

    [Fact]
    public void InspectorBackdrop_ClosesDrawer()
    {
        // Arrange
        var inspectorState = new InspectorState();
        Services.AddScoped(_ => inspectorState);

        var component = Render<MobileInspectorDrawer>();
        var model = new InspectorModel(Title: "Test Item", Fields: new { });
        inspectorState.Show(model);

        // Verify drawer is open
        Assert.NotEmpty(component.FindAll("[data-testid='mobile-inspector']"));

        // Act - click backdrop
        var backdrop = component.Find("[data-testid='mobile-inspector-backdrop']");
        backdrop.Click();

        // Assert - drawer is closed and state is cleared
        Assert.Empty(component.FindAll("[data-testid='mobile-inspector']"));
        Assert.Null(inspectorState.Current);
    }

    [Fact]
    public async Task Navigation_ClosesInspectorDrawer()
    {
        // Arrange
        var inspectorState = new InspectorState();
        Services.AddScoped(_ => inspectorState);

        var navManager = Services.GetRequiredService<NavigationManager>();
        var component = Render<MobileInspectorDrawer>();

        var model = new InspectorModel(Title: "Test Item", Fields: new { });
        inspectorState.Show(model);

        // Verify drawer is open
        Assert.NotEmpty(component.FindAll("[data-testid='mobile-inspector']"));

        // Act - navigate to a different page
        await component.InvokeAsync(() =>
        {
            navManager.NavigateTo("/different-page", replace: true);
        });

        // Assert - drawer is closed
        Assert.Empty(component.FindAll("[data-testid='mobile-inspector']"));
        Assert.Null(inspectorState.Current);
    }

    [Fact]
    public void InspectorPanel_RendersWithinDrawer()
    {
        // Arrange
        var inspectorState = new InspectorState();
        Services.AddScoped(_ => inspectorState);

        var component = Render<MobileInspectorDrawer>();
        var model = new InspectorModel(
            Title: "Test Device",
            Fields: new { DeviceName = "Front Door" },
            RawJson: "{\"id\":123}");
        inspectorState.Show(model);

        // Act / Assert - InspectorPanel should be rendered and functional
        Assert.Contains("Test Device", component.Markup);

        // Verify tabs are present
        var fieldsTabs = component.FindAll("[data-testid='inspector-tab-fields']");
        Assert.NotEmpty(fieldsTabs);
    }
}
