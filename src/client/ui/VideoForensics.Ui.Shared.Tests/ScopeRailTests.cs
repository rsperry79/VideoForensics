namespace VideoForensics.Ui.Shared.Tests;

using AngleSharp.Html.Dom;
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

public class ScopeRail_Rendering_Tests : BunitContext
{
    public ScopeRail_Rendering_Tests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        // Default TimeProvider for tests that don't need a fixed clock; RegisterFakeTime overrides
        // this (later registrations win resolution) for tests asserting exact quick-range dates.
        Services.AddSingleton(TimeProvider.System);

        // Defaults: no open cases, no signed-in operator. Tests that care about case behavior
        // override these with RegisterCaseRepository / RegisterRoleAsync (later registrations win).
        RegisterCaseRepository();
        Services.AddScoped(sp => new CaseState(sp.GetRequiredService<ICaseRepository>(), sp.GetRequiredService<ScopeState>()));
        Services.AddScoped(sp => new PairedSessionState(sp.GetRequiredService<IJSRuntime>()));
    }

    private static Device MakeDevice(string name) => new()
    {
        Id = Guid.NewGuid(),
        LocationId = Guid.NewGuid(),
        ProviderDeviceId = Guid.NewGuid().ToString(),
        Name = name,
        Type = "camera",
    };

    private static ForensicCase MakeCase(
        string caseNumber = "CASE-001",
        string title = "Test Case",
        CaseStatus status = CaseStatus.Open,
        DateTime? scopeFromUtc = null,
        DateTime? scopeToUtc = null) => new()
    {
        Id = Guid.NewGuid(),
        CaseNumber = caseNumber,
        Title = title,
        Status = status,
        ScopeFromUtc = scopeFromUtc,
        ScopeToUtc = scopeToUtc,
        CreatedBy = "test",
        CreatedAtUtc = DateTime.UtcNow
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

    private Mock<ICaseRepository> RegisterCaseRepository(params ForensicCase[] openCases)
    {
        var caseRepository = new Mock<ICaseRepository>();
        caseRepository
            .Setup(r => r.ListAsync(CaseStatus.Open, It.IsAny<CancellationToken>()))
            .ReturnsAsync(openCases.ToList());
        Services.AddScoped(_ => caseRepository.Object);
        return caseRepository;
    }

    private async Task RegisterRoleAsync(OperatorRole role)
    {
        var session = new PairedSessionState(JSInterop.JSRuntime);
        await session.SetAsync("test-token", Guid.NewGuid(), role.ToString());
        Services.AddScoped(_ => session);
    }

    [Fact]
    public void Renders_InitiallyWithDefaultScope()
    {
        // Arrange
        var fixedNow = new DateTime(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);
        RegisterFakeTime(fixedNow);
        RegisterDeviceRepository(MakeDevice("Front Door"));

        // Act
        var component = Render<ScopeRail>();

        // Assert - default scope is last 7 days from "today", shown inclusively.
        var dateInputs = component.FindAll("input[type=date]");
        Assert.Equal(2, dateInputs.Count);
        Assert.Equal("2026-09-17", dateInputs[0].GetAttribute("value"));
        Assert.Equal("2026-09-24", dateInputs[1].GetAttribute("value"));
        Assert.Contains("All devices", component.Markup);
    }

    [Fact]
    public void AppliesInitialQueryString_AndReflectsInControls()
    {
        // Arrange
        var device1 = MakeDevice("Front Door");
        RegisterDeviceRepository(device1);
        Services.AddScoped(_ => new ScopeState());

        var navManager = Services.GetRequiredService<NavigationManager>();
        navManager.NavigateTo($"/?devices={device1.Id:D}&from=2026-09-01&to=2026-09-10", replace: true);

        // Act
        var component = Render<ScopeRail>();

        // Assert - scope state
        var state = Services.GetRequiredService<ScopeState>();
        Assert.Contains(device1.Id, state.Current.DeviceIds);
        Assert.Equal(new DateTime(2026, 9, 1), state.Current.FromUtc);
        Assert.Equal(new DateTime(2026, 9, 10), state.Current.ToUtc);

        // Assert - controls reflect the loaded scope
        var checkbox = (IHtmlInputElement)component.Find($"input[type=checkbox][data-device-id='{device1.Id}']");
        Assert.True(checkbox.IsChecked);

        var dateInputs = component.FindAll("input[type=date]");
        Assert.Equal("2026-09-01", dateInputs[0].GetAttribute("value"));
        // ToUtc (2026-09-10) is an exclusive upper bound; the picker shows the inclusive last day.
        Assert.Equal("2026-09-09", dateInputs[1].GetAttribute("value"));
    }

    [Fact]
    public void DeviceCheckbox_WhenChecked_AddsDeviceToScopeAndUrl()
    {
        // Arrange
        var device1 = MakeDevice("Front Door");
        RegisterDeviceRepository(device1);
        Services.AddScoped(_ => new ScopeState());

        var component = Render<ScopeRail>();

        // Act
        var checkbox = component.Find($"input[type=checkbox][data-device-id='{device1.Id}']");
        checkbox.Change(true);

        // Assert
        var state = Services.GetRequiredService<ScopeState>();
        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.Contains(device1.Id, state.Current.DeviceIds);
        Assert.Contains($"devices={device1.Id:D}", nav.Uri);
    }

    [Fact]
    public void DeviceCheckbox_WhenUnchecked_RemovesDeviceFromScope()
    {
        // Arrange
        var device1 = MakeDevice("Front Door");
        var device2 = MakeDevice("Back Door");
        RegisterDeviceRepository(device1, device2);
        Services.AddScoped(_ => new ScopeState());

        var navManager = Services.GetRequiredService<NavigationManager>();
        navManager.NavigateTo($"/?devices={device1.Id:D},{device2.Id:D}", replace: true);

        var component = Render<ScopeRail>();

        // Act
        var checkbox1 = component.Find($"input[type=checkbox][data-device-id='{device1.Id}']");
        checkbox1.Change(false);

        // Assert
        var state = Services.GetRequiredService<ScopeState>();
        Assert.DoesNotContain(device1.Id, state.Current.DeviceIds);
        Assert.Contains(device2.Id, state.Current.DeviceIds);
    }

    [Fact]
    public void FromDateChanged_UpdatesScopeAndUrl()
    {
        // Arrange
        RegisterDeviceRepository();
        Services.AddScoped(_ => new ScopeState());
        var component = Render<ScopeRail>();

        // Act
        var fromInput = component.FindAll("input[type=date]")[0];
        fromInput.Change("2026-09-01");

        // Assert
        var state = Services.GetRequiredService<ScopeState>();
        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.Equal(new DateTime(2026, 9, 1), state.Current.FromUtc);
        Assert.Contains("from=2026-09-01", nav.Uri);
    }

    [Fact]
    public void ToDateChanged_UpdatesScopeAndUrl_UsingExclusiveUpperBound()
    {
        // Arrange - establish a known baseline range so changing "To" alone can't trip the
        // From/To swap-if-reversed normalization (the new "To" here stays after this "From").
        RegisterDeviceRepository();
        Services.AddScoped(_ => new ScopeState());

        var navManager = Services.GetRequiredService<NavigationManager>();
        navManager.NavigateTo("/?from=2026-09-01&to=2026-09-05", replace: true);

        var component = Render<ScopeRail>();

        // Act - user picks an inclusive "last day" of 2026-09-10
        var toInput = component.FindAll("input[type=date]")[1];
        toInput.Change("2026-09-10");

        // Assert - stored ToUtc is the exclusive upper bound (one day later)
        var state = Services.GetRequiredService<ScopeState>();
        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.Equal(new DateTime(2026, 9, 11), state.Current.ToUtc);
        Assert.Contains("to=2026-09-11", nav.Uri);
    }

    [Fact]
    public void SearchChanged_UpdatesScopeAndUrl()
    {
        // Arrange
        RegisterDeviceRepository();
        Services.AddScoped(_ => new ScopeState());
        var component = Render<ScopeRail>();

        // Act
        var searchInput = component.Find("input.scope-search-input");
        searchInput.Change("front door");

        // Assert
        var state = Services.GetRequiredService<ScopeState>();
        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.Equal("front door", state.Current.Search);
        Assert.Contains("q=", nav.Uri);
    }

    [Fact]
    public void QuickRangeButton_24h_UsesInjectedTimeProviderInDateGranularity()
    {
        // Arrange
        var fixedNow = new DateTime(2026, 9, 24, 15, 30, 0, DateTimeKind.Utc);
        RegisterFakeTime(fixedNow);
        RegisterDeviceRepository();

        var component = Render<ScopeRail>();

        // Act
        var button24h = component.FindAll("button").First(b => b.TextContent.Trim() == "24h");
        button24h.Click();

        // Assert
        var state = Services.GetRequiredService<ScopeState>();
        Assert.Equal(new DateTime(2026, 9, 23), state.Current.FromUtc);
        Assert.Equal(new DateTime(2026, 9, 25), state.Current.ToUtc);
    }

    [Fact]
    public void QuickRangeButton_7d_UsesInjectedTimeProvider_ExactDatesAndUrl()
    {
        // Arrange - start from a different, explicit range so the click is a real change.
        var fixedNow = new DateTime(2026, 9, 24, 15, 30, 0, DateTimeKind.Utc);
        RegisterFakeTime(fixedNow);
        RegisterDeviceRepository();

        var navManager = Services.GetRequiredService<NavigationManager>();
        navManager.NavigateTo("/?from=2026-01-01&to=2026-02-01", replace: true);

        var component = Render<ScopeRail>();

        // Act
        var button7d = component.FindAll("button").First(b => b.TextContent.Trim() == "7d");
        button7d.Click();

        // Assert
        var state = Services.GetRequiredService<ScopeState>();
        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.Equal(new DateTime(2026, 9, 17), state.Current.FromUtc);
        Assert.Equal(new DateTime(2026, 9, 25), state.Current.ToUtc);

        // "7d" recomputes to exactly ForensicScope.Default's own last-7-days window for this "now",
        // so ToQueryString omits from/to as defaults - the URL's explicit date params are cleared.
        Assert.DoesNotContain("from=", nav.Uri);
        Assert.DoesNotContain("to=", nav.Uri);
    }

    [Fact]
    public void QuickRangeButton_30d_UsesInjectedTimeProvider()
    {
        // Arrange
        var fixedNow = new DateTime(2026, 9, 24, 15, 30, 0, DateTimeKind.Utc);
        RegisterFakeTime(fixedNow);
        RegisterDeviceRepository();

        var component = Render<ScopeRail>();

        // Act
        var button30d = component.FindAll("button").First(b => b.TextContent.Trim() == "30d");
        button30d.Click();

        // Assert
        var state = Services.GetRequiredService<ScopeState>();
        Assert.Equal(new DateTime(2026, 8, 25), state.Current.FromUtc);
        Assert.Equal(new DateTime(2026, 9, 25), state.Current.ToUtc);
    }

    [Fact]
    public void ResetButton_ResetsToDefault()
    {
        // Arrange
        RegisterDeviceRepository();
        Services.AddScoped(_ => new ScopeState());

        var navManager = Services.GetRequiredService<NavigationManager>();
        var device1 = Guid.NewGuid();
        navManager.NavigateTo($"/?devices={device1:D}&from=2026-09-01&to=2026-09-10", replace: true);

        var component = Render<ScopeRail>();
        var state = Services.GetRequiredService<ScopeState>();
        Assert.NotEmpty(state.Current.DeviceIds); // Verify it's not default

        // Act
        var resetButton = component.FindAll("button").First(b => b.TextContent.Trim() == "Reset");
        resetButton.Click();

        // Assert
        Assert.Empty(state.Current.DeviceIds);
        var now = DateTime.UtcNow.Date;
        Assert.Equal(now.AddDays(-7), state.Current.FromUtc);
        Assert.Equal(now.AddDays(1), state.Current.ToUtc);
    }

    [Fact]
    public void ResetButton_KeepsUnrelatedQueryKey_RemovesScopeKeys()
    {
        // Arrange
        RegisterDeviceRepository();
        Services.AddScoped(_ => new ScopeState());

        var navManager = Services.GetRequiredService<NavigationManager>();
        var device1 = Guid.NewGuid();
        navManager.NavigateTo($"/?tab=raw&devices={device1:D}&from=2026-09-01&to=2026-09-10", replace: true);

        var component = Render<ScopeRail>();

        // Act
        var resetButton = component.FindAll("button").First(b => b.TextContent.Trim() == "Reset");
        resetButton.Click();

        // Assert
        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.Contains("tab=raw", nav.Uri);
        Assert.DoesNotContain("devices=", nav.Uri);
        Assert.DoesNotContain("from=", nav.Uri);
        Assert.DoesNotContain("to=", nav.Uri);
    }

    [Fact]
    public void DeviceRepositoryError_ShowsErrorMessage()
    {
        // Arrange
        var deviceRepository = new Mock<IDeviceRepository>();
        deviceRepository
            .Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Test error"));

        Services.AddScoped(_ => new ScopeState());
        Services.AddScoped(_ => deviceRepository.Object);

        // Act
        var component = Render<ScopeRail>();

        // Assert
        var markup = component.Markup;
        Assert.Contains("Devices unavailable", markup);
    }

    [Fact]
    public void CasePicker_ListsOpenCases_AndNoCaseOption()
    {
        // Arrange
        RegisterDeviceRepository();
        Services.AddScoped(_ => new ScopeState());
        var case1 = MakeCase(caseNumber: "CASE-100", title: "Alpha");
        var case2 = MakeCase(caseNumber: "CASE-200", title: "Beta");
        RegisterCaseRepository(case1, case2);

        // Act
        var component = Render<ScopeRail>();

        // Assert
        var picker = component.Find("[data-testid='case-picker']");
        var optionTexts = picker.Children.Select(c => c.TextContent.Trim()).ToList();
        Assert.Contains("(no case)", optionTexts);
        Assert.Contains("CASE-100 · Alpha", optionTexts);
        Assert.Contains("CASE-200 · Beta", optionTexts);
    }

    [Fact]
    public async Task CasePicker_Selecting_ActivatesCase_AndUrlHasCaseAndScopeKeys()
    {
        // Arrange
        RegisterDeviceRepository();
        Services.AddScoped(_ => new ScopeState());
        var device1 = Guid.NewGuid();
        var device2 = Guid.NewGuid();
        var testCase = MakeCase();
        var caseRepository = RegisterCaseRepository(testCase);
        caseRepository
            .Setup(r => r.GetAsync(testCase.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(testCase);
        caseRepository
            .Setup(r => r.GetDeviceIdsAsync(testCase.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { device1, device2 }.ToList());

        var component = Render<ScopeRail>();

        // Act
        var picker = (IHtmlSelectElement)component.Find("[data-testid='case-picker']");
        await component.InvokeAsync(() => picker.Change(testCase.Id.ToString("D")));

        // Assert
        var scopeState = Services.GetRequiredService<ScopeState>();
        Assert.Equal(new[] { device1, device2 }, scopeState.Current.DeviceIds);

        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.Contains($"case={testCase.Id:D}", nav.Uri);
        Assert.Contains("devices=", nav.Uri);
    }

    [Fact]
    public async Task InitialCaseQueryString_ActivatesCaseOnLoad()
    {
        // Arrange
        RegisterDeviceRepository();
        Services.AddScoped(_ => new ScopeState());
        var device1 = Guid.NewGuid();
        var testCase = MakeCase();
        var caseRepository = RegisterCaseRepository(testCase);
        caseRepository
            .Setup(r => r.GetAsync(testCase.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(testCase);
        caseRepository
            .Setup(r => r.GetDeviceIdsAsync(testCase.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { device1 }.ToList());

        var navManager = Services.GetRequiredService<NavigationManager>();
        navManager.NavigateTo($"/?case={testCase.Id:D}", replace: true);

        // Act
        var component = Render<ScopeRail>();
        await Task.Yield();

        // Assert
        var caseState = Services.GetRequiredService<CaseState>();
        var scopeState = Services.GetRequiredService<ScopeState>();
        Assert.NotNull(caseState.ActiveCase);
        Assert.Equal(testCase.Id, caseState.ActiveCase!.Id);
        Assert.Contains(device1, scopeState.Current.DeviceIds);
    }

    [Fact]
    public async Task SaveScopeToCase_ShownForReview_HiddenForReadOnly_AndCallsSetScope()
    {
        // Arrange
        RegisterDeviceRepository();
        Services.AddScoped(_ => new ScopeState());
        var testCase = MakeCase(scopeFromUtc: new DateTime(2026, 1, 1), scopeToUtc: new DateTime(2026, 2, 1));
        var caseRepository = RegisterCaseRepository(testCase);
        caseRepository
            .Setup(r => r.GetAsync(testCase.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(testCase);
        caseRepository
            .Setup(r => r.GetDeviceIdsAsync(testCase.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid>());
        caseRepository
            .Setup(r => r.SetScopeAsync(
                testCase.Id,
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<IReadOnlyCollection<Guid>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await RegisterRoleAsync(OperatorRole.Review);

        var component = Render<ScopeRail>();
        var picker = (IHtmlSelectElement)component.Find("[data-testid='case-picker']");
        await component.InvokeAsync(() => picker.Change(testCase.Id.ToString("D")));

        // Act - change a date so scope diverges from the case's saved (unset) scope
        var fromInput = component.FindAll("input[type=date]")[0];
        fromInput.Change("2026-09-01");

        // Assert - save button appears for Review
        var saveButton = component.Find("[data-testid='save-scope-to-case']");
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
        RegisterDeviceRepository();
        Services.AddScoped(_ => new ScopeState());
        var testCase = MakeCase();
        var caseRepository = RegisterCaseRepository(testCase);
        caseRepository
            .Setup(r => r.GetAsync(testCase.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(testCase);
        caseRepository
            .Setup(r => r.GetDeviceIdsAsync(testCase.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid>());

        await RegisterRoleAsync(OperatorRole.ReadOnly);

        var component = Render<ScopeRail>();
        var picker = (IHtmlSelectElement)component.Find("[data-testid='case-picker']");
        await component.InvokeAsync(() => picker.Change(testCase.Id.ToString("D")));

        // Act - change a date so scope diverges from the case
        var fromInput = component.FindAll("input[type=date]")[0];
        fromInput.Change("2026-09-01");

        // Assert
        Assert.Empty(component.FindAll("[data-testid='save-scope-to-case']"));
    }

    [Fact]
    public async Task CasePicker_SelectingNoCase_RemovesCaseKey_KeepsOtherKeys()
    {
        // Arrange
        RegisterDeviceRepository();
        Services.AddScoped(_ => new ScopeState());
        var testCase = MakeCase();
        var caseRepository = RegisterCaseRepository(testCase);
        caseRepository
            .Setup(r => r.GetAsync(testCase.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(testCase);
        caseRepository
            .Setup(r => r.GetDeviceIdsAsync(testCase.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guid>());

        var navManager = Services.GetRequiredService<NavigationManager>();
        navManager.NavigateTo($"/?tab=raw&case={testCase.Id:D}", replace: true);

        var component = Render<ScopeRail>();
        await Task.Yield();

        // Act
        var picker = (IHtmlSelectElement)component.Find("[data-testid='case-picker']");
        await component.InvokeAsync(() => picker.Change(string.Empty));

        // Assert
        var nav = Services.GetRequiredService<NavigationManager>();
        Assert.DoesNotContain("case=", nav.Uri);
        Assert.Contains("tab=raw", nav.Uri);

        var caseState = Services.GetRequiredService<CaseState>();
        Assert.Null(caseState.ActiveCase);
    }

    [Fact]
    public void CaseRepositoryError_ShowsCasesUnavailable()
    {
        // Arrange
        RegisterDeviceRepository();
        Services.AddScoped(_ => new ScopeState());
        var caseRepository = new Mock<ICaseRepository>();
        caseRepository
            .Setup(r => r.ListAsync(CaseStatus.Open, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Test error"));
        Services.AddScoped(_ => caseRepository.Object);

        // Act
        var component = Render<ScopeRail>();

        // Assert
        Assert.Contains("Cases unavailable", component.Markup);
    }
}
