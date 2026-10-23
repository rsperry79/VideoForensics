
using AngleSharp.Dom;

using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;

using Moq;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Ui.Shared.Layout.Mobile;
using VideoForensics.Ui.Shared.Resources;
using VideoForensics.Ui.Shared.Services;
using VideoForensics.Ui.Shared.Services.Cases;
using VideoForensics.Ui.Shared.Services.Inspector;
using VideoForensics.Ui.Shared.Services.Scope;

using Xunit;

namespace VideoForensics.Ui.Shared.Tests;
/// <summary>
/// Tests for MobileFiltersSheet component.
/// </summary>
public class MobileFiltersSheet_Rendering_Tests : ScopeRailTestContext
{
    public MobileFiltersSheet_Rendering_Tests()
    {
        // Register Syncfusion services required by SfButton
        _ = Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        _ = Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        _ = Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();

        // Don't add ScopeState here - let each test register it as needed (some tests override ScopeState).
        // Base registers CaseState, PairedSessionState, case/device repositories and TimeProvider defaults.
        _ = RegisterDeviceRepository();

        _ = Services.AddScoped<IStringLocalizer<SharedResources>>(_ => TestLocalizer.Create());
    }

    [Fact]
    public void FiltersButton_IsPresent()
    {
        // Arrange
        _ = Services.AddScoped(sp => new ScopeState());  // Register default ScopeState
        _ = RegisterDeviceRepository();

        // Act
        IRenderedComponent<MobileFiltersSheet> component = Render<MobileFiltersSheet>();

        // Assert
        IReadOnlyList<IElement> filtersButton = component.FindAll("[data-testid='mobile-filters-toggle']");
        Assert.NotEmpty(filtersButton);
    }

    [Fact]
    public void FiltersButton_TogglesSheet()
    {
        // Arrange
        _ = Services.AddScoped(sp => new ScopeState());  // Register default ScopeState
        _ = RegisterDeviceRepository();

        IRenderedComponent<MobileFiltersSheet> component = Render<MobileFiltersSheet>();

        // Act - click filters button to open sheet
        IElement filtersButton = component.Find("[data-testid='mobile-filters-toggle']");
        filtersButton.Click();

        // Assert - sheet is visible and contains ScopeRail markup
        IReadOnlyList<IElement> sheet = component.FindAll("[data-testid='mobile-filters-sheet']");
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
        _ = Services.AddScoped(sp => new ScopeState());  // Register default ScopeState
        _ = RegisterDeviceRepository();

        IRenderedComponent<MobileFiltersSheet> component = Render<MobileFiltersSheet>();
        IElement filtersButton = component.Find("[data-testid='mobile-filters-toggle']");
        filtersButton.Click();

        // Act - click close button
        IElement closeButton = component.Find("[data-testid='mobile-filters-close']");
        closeButton.Click();

        // Assert - sheet is closed
        Assert.Empty(component.FindAll("[data-testid='mobile-filters-sheet']"));
    }

    [Fact]
    public void FiltersSheetBackdrop_ClosesSheet()
    {
        // Arrange
        _ = Services.AddScoped(sp => new ScopeState());  // Register default ScopeState
        _ = RegisterDeviceRepository();

        IRenderedComponent<MobileFiltersSheet> component = Render<MobileFiltersSheet>();
        IElement filtersButton = component.Find("[data-testid='mobile-filters-toggle']");
        filtersButton.Click();

        // Act - click backdrop
        IElement backdrop = component.Find("[data-testid='mobile-filters-backdrop']");
        backdrop.Click();

        // Assert - sheet is closed
        Assert.Empty(component.FindAll("[data-testid='mobile-filters-sheet']"));
    }

    [Fact]
    public void FiltersIndicator_DoesNotAppearWithDefaultScope()
    {
        // Arrange
        var fixedNow = new DateTime(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);
        _ = RegisterFakeTime(fixedNow);
        _ = RegisterDeviceRepository();

        // Act
        IRenderedComponent<MobileFiltersSheet> component = Render<MobileFiltersSheet>();

        // Assert - indicator should not be present with default scope
        Assert.Empty(component.FindAll("[data-testid='mobile-filters-indicator']"));
    }

    [Fact]
    public async Task FiltersIndicator_AppearsWhenDeviceSelected()
    {
        // Arrange
        var fixedNow = new DateTime(2026, 9, 25, 12, 0, 0, DateTimeKind.Utc);
        FakeTimeProvider timeProvider = RegisterFakeTime(fixedNow);
        Device device = MakeDevice("Test Camera");
        _ = RegisterDeviceRepository(device);

        var scopeState = new ScopeState(timeProvider);
        _ = Services.AddScoped(_ => scopeState);

        IRenderedComponent<MobileFiltersSheet> component = Render<MobileFiltersSheet>();

        // Act - update scope to include a device
        var customScope = new ForensicScope(
            DeviceIds: [device.Id],
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
        _ = Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        _ = Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        _ = Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();

        // Register services needed by MobileInspectorDrawer
        _ = Services.AddScoped(sp => new InspectorState());
        _ = Services.AddScoped(sp => new CaseState(sp.GetRequiredService<ICaseRepository>(), sp.GetRequiredService<ScopeState>()));
        _ = Services.AddScoped(sp => new PairedSessionState(sp.GetRequiredService<IJSRuntime>()));
        _ = Services.AddScoped(sp => new ScopeState());

        // Mock repositories
        var caseRepoMock = new Mock<ICaseRepository>();
        _ = Services.AddScoped(_ => caseRepoMock.Object);

        // InspectorPanel's evidence-actions section (hold/release/verify) needs these too, even
        // though none of these tests exercise them.
        _ = Services.AddScoped(_ => new Mock<ILegalHoldRepository>().Object);
        _ = Services.AddScoped(_ => new Mock<VideoForensics.Client.Common.Contracts.IEvidenceValidationService>().Object);

        _ = Services.AddScoped<IStringLocalizer<SharedResources>>(_ => TestLocalizer.Create());

        _ = Services.AddSingleton(TimeProvider.System);
    }

    [Fact]
    public void InspectorClosed_WhenInspectorStateNull()
    {
        // Arrange
        var inspectorState = new InspectorState();
        _ = Services.AddScoped(_ => inspectorState);

        IRenderedComponent<MobileInspectorDrawer> component = Render<MobileInspectorDrawer>();

        // Act / Assert - inspector drawer should not be visible
        Assert.Empty(component.FindAll("[data-testid='mobile-inspector']"));
    }

    [Fact]
    public async Task InspectorDrawer_OpensWhenStateUpdated()
    {
        // Arrange
        var inspectorState = new InspectorState();
        _ = Services.AddScoped(_ => inspectorState);

        IRenderedComponent<MobileInspectorDrawer> component = Render<MobileInspectorDrawer>();
        var model = new InspectorModel(Title: "Test Item", Fields: new { Id = 1 });

        // Act
        await component.InvokeAsync(() =>
        {
            inspectorState.Show(model);
        });

        // Assert - inspector drawer should appear with the model
        IReadOnlyList<IElement> drawer = component.FindAll("[data-testid='mobile-inspector']");
        Assert.NotEmpty(drawer);
        Assert.Contains("Test Item", component.Markup);
    }

    [Fact]
    public void InspectorCloseButton_ClearsState()
    {
        // Arrange
        var inspectorState = new InspectorState();
        _ = Services.AddScoped(_ => inspectorState);

        IRenderedComponent<MobileInspectorDrawer> component = Render<MobileInspectorDrawer>();
        var model = new InspectorModel(Title: "Test Item", Fields: new { });
        inspectorState.Show(model);

        // Verify drawer is open
        Assert.NotEmpty(component.FindAll("[data-testid='mobile-inspector']"));

        // Act - click close button
        IElement closeButton = component.Find("[data-testid='inspector-close']");
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
        _ = Services.AddScoped(_ => inspectorState);

        IRenderedComponent<MobileInspectorDrawer> component = Render<MobileInspectorDrawer>();
        var model = new InspectorModel(Title: "Test Item", Fields: new { });
        inspectorState.Show(model);

        // Verify drawer is open
        Assert.NotEmpty(component.FindAll("[data-testid='mobile-inspector']"));

        // Act - click backdrop
        IElement backdrop = component.Find("[data-testid='mobile-inspector-backdrop']");
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
        _ = Services.AddScoped(_ => inspectorState);

        NavigationManager navManager = Services.GetRequiredService<NavigationManager>();
        IRenderedComponent<MobileInspectorDrawer> component = Render<MobileInspectorDrawer>();

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
        _ = Services.AddScoped(_ => inspectorState);

        IRenderedComponent<MobileInspectorDrawer> component = Render<MobileInspectorDrawer>();
        var model = new InspectorModel(
            Title: "Test Device",
            Fields: new { DeviceName = "Front Door" },
            RawJson: "{\"id\":123}");
        inspectorState.Show(model);

        // Act / Assert - InspectorPanel should be rendered and functional
        Assert.Contains("Test Device", component.Markup);

        // Verify tabs are present
        IReadOnlyList<IElement> fieldsTabs = component.FindAll("[data-testid='inspector-tab-fields']");
        Assert.NotEmpty(fieldsTabs);
    }
}
