
using AngleSharp.Dom;
using AngleSharp.Html.Dom;

using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

using Moq;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Client.Ui.Shared.Components.Scope;
using VideoForensics.Client.Ui.Shared.Services.Cases;
using VideoForensics.Client.Ui.Shared.Services.Scope;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

public class ScopeRail_Rendering_Tests : ScopeRailTestContext
{
    [Fact]
    public void Renders_InitiallyWithDefaultScope()
    {
        // Arrange
        var fixedNow = new DateTime(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);
        _ = RegisterFakeTime(fixedNow);
        _ = RegisterDeviceRepository(MakeDevice("Front Door"));

        // Act
        IRenderedComponent<ScopeRail> component = Render<ScopeRail>();

        // Assert - the default scope is the last 7 days, a preset, so the dropdown shows it and no date pickers appear.
        Assert.True(component.Find("[data-testid='scope-range'] option[value='week']").HasAttribute("selected"));
        Assert.Empty(component.FindAll("input[type=date]"));
        Assert.Contains("All devices", component.Markup);
    }

    [Fact]
    public void RangeDropdown_Custom_ShowsFromThenToPickersInOneColumn_WithInclusiveDates()
    {
        // Arrange
        var fixedNow = new DateTime(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);
        _ = RegisterFakeTime(fixedNow);
        _ = RegisterDeviceRepository(MakeDevice("Front Door"));
        IRenderedComponent<ScopeRail> component = Render<ScopeRail>();

        // Act
        component.Find("[data-testid='scope-range']").Change("custom");

        // Assert - From label and picker, then To label and picker, stacked
        IElement dates = component.Find(".scope-range-dates");
        Assert.Contains("flex-column", dates.ClassName);
        Assert.Equal(new[] { "scope-range-from", "scope-range-to" }, dates.QuerySelectorAll("input").Select(i => i.GetAttribute("data-testid")).ToArray());
        Assert.Equal("2026-09-17", component.Find("[data-testid='scope-range-from']").GetAttribute("value"));
        Assert.Equal("2026-09-24", component.Find("[data-testid='scope-range-to']").GetAttribute("value"));
    }

    [Fact]
    public void Rail_SectionsAreInTheOrderTimeRangeFiltersCase_EachCollapsible()
    {
        // Arrange
        _ = RegisterDeviceRepository(MakeDevice("Front Door"));

        // Act
        IRenderedComponent<ScopeRail> component = Render<ScopeRail>();

        // Assert
        string[] titles = component.FindAll(".review-sidebar-section-header .fw-semibold").Select(e => e.TextContent).ToArray();
        Assert.Equal(new[] { "Time range", "Filters", "Case" }, titles);
        Assert.Empty(component.FindAll("input.scope-search-input"));
    }

    [Fact]
    public void AppliesInitialQueryString_AndReflectsInControls()
    {
        // Arrange
        Device device1 = MakeDevice("Front Door");
        _ = RegisterDeviceRepository(device1);
        _ = Services.AddScoped(_ => new ScopeState());

        NavigationManager navManager = Services.GetRequiredService<NavigationManager>();
        navManager.NavigateTo($"/?devices={device1.Id:D}&from=2026-09-01&to=2026-09-10", replace: true);

        // Act
        IRenderedComponent<ScopeRail> component = Render<ScopeRail>();

        // Assert - scope state
        ScopeState state = Services.GetRequiredService<ScopeState>();
        Assert.Contains(device1.Id, state.Current.DeviceIds);
        Assert.Equal(new DateTime(2026, 9, 1), state.Current.FromUtc);
        Assert.Equal(new DateTime(2026, 9, 10), state.Current.ToUtc);

        // Assert - controls reflect the loaded scope (the device list opens from its button)
        component.Find("[data-testid='scope-device-button']").Click();
        var checkbox = (IHtmlInputElement)component.Find($"input[type=checkbox][data-device-id='{device1.Id}']");
        Assert.True(checkbox.IsChecked);

        IReadOnlyList<IElement> dateInputs = component.FindAll("input[type=date]");
        Assert.Equal("2026-09-01", dateInputs[0].GetAttribute("value"));
        // ToUtc (2026-09-10) is an exclusive upper bound; the picker shows the inclusive last day.
        Assert.Equal("2026-09-09", dateInputs[1].GetAttribute("value"));
    }

    [Fact]
    public void DeviceCheckbox_WhenChecked_AddsDeviceToScopeAndUrl()
    {
        // Arrange
        Device device1 = MakeDevice("Front Door");
        _ = RegisterDeviceRepository(device1);
        _ = Services.AddScoped(_ => new ScopeState());

        IRenderedComponent<ScopeRail> component = Render<ScopeRail>();

        // Act
        component.Find("[data-testid='scope-device-button']").Click();
        IElement checkbox = component.Find($"input[type=checkbox][data-device-id='{device1.Id}']");
        checkbox.Change(true);

        // Assert
        ScopeState state = Services.GetRequiredService<ScopeState>();
        NavigationManager nav = Services.GetRequiredService<NavigationManager>();
        Assert.Contains(device1.Id, state.Current.DeviceIds);
        Assert.Contains($"devices={device1.Id:D}", nav.Uri);
    }

    [Fact]
    public void DeviceCheckbox_WhenUnchecked_RemovesDeviceFromScope()
    {
        // Arrange
        Device device1 = MakeDevice("Front Door");
        Device device2 = MakeDevice("Back Door");
        _ = RegisterDeviceRepository(device1, device2);
        _ = Services.AddScoped(_ => new ScopeState());

        NavigationManager navManager = Services.GetRequiredService<NavigationManager>();
        navManager.NavigateTo($"/?devices={device1.Id:D},{device2.Id:D}", replace: true);

        IRenderedComponent<ScopeRail> component = Render<ScopeRail>();

        // Act
        component.Find("[data-testid='scope-device-button']").Click();
        IElement checkbox1 = component.Find($"input[type=checkbox][data-device-id='{device1.Id}']");
        checkbox1.Change(false);

        // Assert
        ScopeState state = Services.GetRequiredService<ScopeState>();
        Assert.DoesNotContain(device1.Id, state.Current.DeviceIds);
        Assert.Contains(device2.Id, state.Current.DeviceIds);
    }

    [Fact]
    public void FromDateChanged_UpdatesScopeAndUrl()
    {
        // Arrange
        _ = RegisterDeviceRepository();
        _ = Services.AddScoped(_ => new ScopeState());
        IRenderedComponent<ScopeRail> component = Render<ScopeRail>();

        // Act - the date pickers appear once Custom is chosen
        component.Find("[data-testid='scope-range']").Change("custom");
        IElement fromInput = component.FindAll("input[type=date]")[0];
        fromInput.Change("2026-09-01");

        // Assert
        ScopeState state = Services.GetRequiredService<ScopeState>();
        NavigationManager nav = Services.GetRequiredService<NavigationManager>();
        Assert.Equal(new DateTime(2026, 9, 1), state.Current.FromUtc);
        Assert.Contains("from=2026-09-01", nav.Uri);
    }

    [Fact]
    public void ToDateChanged_UpdatesScopeAndUrl_UsingExclusiveUpperBound()
    {
        // Arrange - establish a known baseline range so changing "To" alone can't trip the
        // From/To swap-if-reversed normalization (the new "To" here stays after this "From").
        _ = RegisterDeviceRepository();
        _ = Services.AddScoped(_ => new ScopeState());

        NavigationManager navManager = Services.GetRequiredService<NavigationManager>();
        navManager.NavigateTo("/?from=2026-09-01&to=2026-09-05", replace: true);

        IRenderedComponent<ScopeRail> component = Render<ScopeRail>();

        // Act - user picks an inclusive "last day" of 2026-09-10
        IElement toInput = component.FindAll("input[type=date]")[1];
        toInput.Change("2026-09-10");

        // Assert - stored ToUtc is the exclusive upper bound (one day later)
        ScopeState state = Services.GetRequiredService<ScopeState>();
        NavigationManager nav = Services.GetRequiredService<NavigationManager>();
        Assert.Equal(new DateTime(2026, 9, 11), state.Current.ToUtc);
        Assert.Contains("to=2026-09-11", nav.Uri);
    }

    [Fact]
    public void RangeDropdown_24h_UsesInjectedTimeProviderInDateGranularity()
    {
        // Arrange
        var fixedNow = new DateTime(2026, 9, 24, 15, 30, 0, DateTimeKind.Utc);
        _ = RegisterFakeTime(fixedNow);
        _ = RegisterDeviceRepository();

        IRenderedComponent<ScopeRail> component = Render<ScopeRail>();

        // Act
        component.Find("[data-testid='scope-range']").Change("day");

        // Assert
        ScopeState state = Services.GetRequiredService<ScopeState>();
        Assert.Equal(new DateTime(2026, 9, 23), state.Current.FromUtc);
        Assert.Equal(new DateTime(2026, 9, 25), state.Current.ToUtc);
    }

    [Fact]
    public void RangeDropdown_7d_UsesInjectedTimeProvider_ExactDatesAndUrl()
    {
        // Arrange - start from a different, explicit range so the click is a real change.
        var fixedNow = new DateTime(2026, 9, 24, 15, 30, 0, DateTimeKind.Utc);
        _ = RegisterFakeTime(fixedNow);
        _ = RegisterDeviceRepository();

        NavigationManager navManager = Services.GetRequiredService<NavigationManager>();
        navManager.NavigateTo("/?from=2026-01-01&to=2026-02-01", replace: true);

        IRenderedComponent<ScopeRail> component = Render<ScopeRail>();

        // Act
        component.Find("[data-testid='scope-range']").Change("week");

        // Assert
        ScopeState state = Services.GetRequiredService<ScopeState>();
        NavigationManager nav = Services.GetRequiredService<NavigationManager>();
        Assert.Equal(new DateTime(2026, 9, 17), state.Current.FromUtc);
        Assert.Equal(new DateTime(2026, 9, 25), state.Current.ToUtc);

        // "7d" recomputes to exactly ForensicScope.Default's own last-7-days window for this "now",
        // so ToQueryString omits from/to as defaults - the URL's explicit date params are cleared.
        Assert.DoesNotContain("from=", nav.Uri);
        Assert.DoesNotContain("to=", nav.Uri);
    }

    [Fact]
    public void RangeDropdown_30d_UsesInjectedTimeProvider()
    {
        // Arrange
        var fixedNow = new DateTime(2026, 9, 24, 15, 30, 0, DateTimeKind.Utc);
        _ = RegisterFakeTime(fixedNow);
        _ = RegisterDeviceRepository();

        IRenderedComponent<ScopeRail> component = Render<ScopeRail>();

        // Act
        component.Find("[data-testid='scope-range']").Change("month");

        // Assert
        ScopeState state = Services.GetRequiredService<ScopeState>();
        Assert.Equal(new DateTime(2026, 8, 25), state.Current.FromUtc);
        Assert.Equal(new DateTime(2026, 9, 25), state.Current.ToUtc);
    }

    [Fact]
    public void DeviceRepositoryError_ShowsErrorMessage()
    {
        // Arrange
        var deviceRepository = new Mock<IDeviceRepository>();
        _ = deviceRepository
            .Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Test error"));

        _ = Services.AddScoped(_ => new ScopeState());
        _ = Services.AddScoped(_ => deviceRepository.Object);

        // Act
        IRenderedComponent<ScopeRail> component = Render<ScopeRail>();

        // Assert
        string markup = component.Markup;
        Assert.Contains("Devices unavailable", markup);
    }

    [Fact]
    public void CasePicker_ListsOpenCases_AndNoCaseOption()
    {
        // Arrange
        _ = RegisterDeviceRepository();
        _ = Services.AddScoped(_ => new ScopeState());
        ForensicCase case1 = MakeCase(caseNumber: "CASE-100", title: "Alpha");
        ForensicCase case2 = MakeCase(caseNumber: "CASE-200", title: "Beta");
        _ = RegisterCaseRepository(case1, case2);

        // Act
        IRenderedComponent<ScopeRail> component = Render<ScopeRail>();

        // Assert
        IElement picker = component.Find("[data-testid='case-picker']");
        var optionTexts = picker.Children.Select(c => c.TextContent.Trim()).ToList();
        Assert.Contains("(no case)", optionTexts);
        Assert.Contains("CASE-100 · Alpha", optionTexts);
        Assert.Contains("CASE-200 · Beta", optionTexts);
    }

    [Fact]
    public async Task CasePicker_Selecting_ActivatesCase_AndUrlHasCaseAndScopeKeys()
    {
        // Arrange
        _ = RegisterDeviceRepository();
        _ = Services.AddScoped(_ => new ScopeState());
        var device1 = Guid.NewGuid();
        var device2 = Guid.NewGuid();
        ForensicCase testCase = MakeCase();
        Mock<ICaseRepository> caseRepository = RegisterCaseRepository(testCase);
        _ = caseRepository
            .Setup(r => r.GetAsync(testCase.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(testCase);
        _ = caseRepository
            .Setup(r => r.GetDeviceIdsAsync(testCase.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { device1, device2 }.ToList());

        IRenderedComponent<ScopeRail> component = Render<ScopeRail>();

        // Act
        var picker = (IHtmlSelectElement)component.Find("[data-testid='case-picker']");
        await component.InvokeAsync(() => picker.Change(testCase.Id.ToString("D")));

        // Assert
        ScopeState scopeState = Services.GetRequiredService<ScopeState>();
        Assert.Equal(new[] { device1, device2 }, scopeState.Current.DeviceIds);

        NavigationManager nav = Services.GetRequiredService<NavigationManager>();
        Assert.Contains($"case={testCase.Id:D}", nav.Uri);
        Assert.Contains("devices=", nav.Uri);
    }

    [Fact]
    public async Task InitialCaseQueryString_ActivatesCaseOnLoad()
    {
        // Arrange
        _ = RegisterDeviceRepository();
        _ = Services.AddScoped(_ => new ScopeState());
        var device1 = Guid.NewGuid();
        ForensicCase testCase = MakeCase();
        Mock<ICaseRepository> caseRepository = RegisterCaseRepository(testCase);
        _ = caseRepository
            .Setup(r => r.GetAsync(testCase.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(testCase);
        _ = caseRepository
            .Setup(r => r.GetDeviceIdsAsync(testCase.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { device1 }.ToList());

        NavigationManager navManager = Services.GetRequiredService<NavigationManager>();
        navManager.NavigateTo($"/?case={testCase.Id:D}", replace: true);

        // Act
        IRenderedComponent<ScopeRail> component = Render<ScopeRail>();
        await Task.Yield();

        // Assert
        CaseState caseState = Services.GetRequiredService<CaseState>();
        ScopeState scopeState = Services.GetRequiredService<ScopeState>();
        Assert.NotNull(caseState.ActiveCase);
        Assert.Equal(testCase.Id, caseState.ActiveCase!.Id);
        Assert.Contains(device1, scopeState.Current.DeviceIds);
    }

    [Fact]
    public async Task SaveScopeToCase_ShownForReview_HiddenForReadOnly_AndCallsSetScope()
    {
        // Arrange
        _ = RegisterDeviceRepository();
        _ = Services.AddScoped(_ => new ScopeState());
        ForensicCase testCase = MakeCase(scopeFromUtc: new DateTime(2026, 1, 1), scopeToUtc: new DateTime(2026, 2, 1));
        Mock<ICaseRepository> caseRepository = RegisterCaseRepository(testCase);
        _ = caseRepository
            .Setup(r => r.GetAsync(testCase.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(testCase);
        _ = caseRepository
            .Setup(r => r.GetDeviceIdsAsync(testCase.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _ = caseRepository
            .Setup(r => r.SetScopeAsync(
                testCase.Id,
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<IReadOnlyCollection<Guid>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await RegisterRoleAsync(OperatorRole.Review);

        IRenderedComponent<ScopeRail> component = Render<ScopeRail>();
        var picker = (IHtmlSelectElement)component.Find("[data-testid='case-picker']");
        await component.InvokeAsync(() => picker.Change(testCase.Id.ToString("D")));

        // Act - change a date so scope diverges from the case's saved (unset) scope
        IElement fromInput = component.FindAll("input[type=date]")[0];
        fromInput.Change("2026-09-01");

        // Assert - save button appears for Review
        IElement saveButton = component.Find("[data-testid='save-scope-to-case']");
        Assert.NotNull(saveButton);

        saveButton.Click();

        caseRepository.Verify(r => r.SetScopeAsync(
            testCase.Id,
            It.IsAny<DateTime?>(),
            It.IsAny<DateTime?>(),
            It.IsAny<IReadOnlyCollection<Guid>>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SaveScopeToCase_NotShownForReadOnly()
    {
        // Arrange
        _ = RegisterDeviceRepository();
        _ = Services.AddScoped(_ => new ScopeState());
        ForensicCase testCase = MakeCase();
        Mock<ICaseRepository> caseRepository = RegisterCaseRepository(testCase);
        _ = caseRepository
            .Setup(r => r.GetAsync(testCase.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(testCase);
        _ = caseRepository
            .Setup(r => r.GetDeviceIdsAsync(testCase.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await RegisterRoleAsync(OperatorRole.ReadOnly);

        IRenderedComponent<ScopeRail> component = Render<ScopeRail>();
        var picker = (IHtmlSelectElement)component.Find("[data-testid='case-picker']");
        await component.InvokeAsync(() => picker.Change(testCase.Id.ToString("D")));

        // Act - change a date so scope diverges from the case (the date pickers appear once Custom is chosen)
        component.Find("[data-testid='scope-range']").Change("custom");
        IElement fromInput = component.FindAll("input[type=date]")[0];
        fromInput.Change("2026-09-01");

        // Assert
        Assert.Empty(component.FindAll("[data-testid='save-scope-to-case']"));
    }

    [Fact]
    public async Task CasePicker_SelectingNoCase_RemovesCaseKey_KeepsOtherKeys()
    {
        // Arrange
        _ = RegisterDeviceRepository();
        _ = Services.AddScoped(_ => new ScopeState());
        ForensicCase testCase = MakeCase();
        Mock<ICaseRepository> caseRepository = RegisterCaseRepository(testCase);
        _ = caseRepository
            .Setup(r => r.GetAsync(testCase.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(testCase);
        _ = caseRepository
            .Setup(r => r.GetDeviceIdsAsync(testCase.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        NavigationManager navManager = Services.GetRequiredService<NavigationManager>();
        navManager.NavigateTo($"/?tab=raw&case={testCase.Id:D}", replace: true);

        IRenderedComponent<ScopeRail> component = Render<ScopeRail>();
        await Task.Yield();

        // Act
        var picker = (IHtmlSelectElement)component.Find("[data-testid='case-picker']");
        await component.InvokeAsync(() => picker.Change(string.Empty));

        // Assert
        NavigationManager nav = Services.GetRequiredService<NavigationManager>();
        Assert.DoesNotContain("case=", nav.Uri);
        Assert.Contains("tab=raw", nav.Uri);

        CaseState caseState = Services.GetRequiredService<CaseState>();
        Assert.Null(caseState.ActiveCase);
    }

    [Fact]
    public void CaseRepositoryError_ShowsCasesUnavailable()
    {
        // Arrange
        _ = RegisterDeviceRepository();
        _ = Services.AddScoped(_ => new ScopeState());
        var caseRepository = new Mock<ICaseRepository>();
        _ = caseRepository
            .Setup(r => r.ListAsync(CaseStatus.Open, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Test error"));
        _ = Services.AddScoped(_ => caseRepository.Object);

        // Act
        IRenderedComponent<ScopeRail> component = Render<ScopeRail>();

        // Assert
        Assert.Contains("Cases unavailable", component.Markup);
    }
}
