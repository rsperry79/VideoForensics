namespace VideoForensics.Ui.Shared.Tests;

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Moq;
using Xunit;
using VideoForensics.Client.Core.Tools;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using Device = VideoForensics.Data.Common.Entities.Device;
using VideoForensics.Data.Core.Contracts;
using VideoForensics.Data.Core.Models;
using VideoForensics.Providers.Common.Contracts;
using VideoForensics.Ui.Shared.Pages;
using VideoForensics.Ui.Shared.Services.Scope;

/// <summary>
/// Shared setup for Analyze.razor + its four Components/Analysis panels: a signed-in auth
/// service, an empty device list by default, a real <see cref="ScopeState"/>, a
/// <see cref="IReportGenerationService"/> mock stubbed to return empty (no-row) reports so no
/// panel ever needs to render a Syncfusion grid, and a real <see cref="JammingToolsOrchestrator"/>
/// (a concrete class - the testable seam is its constructor dependencies: <see
/// cref="IJammingRepository"/> and <see cref="IDeviceHealthRepository"/>, both mocked) wired for
/// an empty health history (fewer than 3 readings -> no incidents detected, so JammingPanel's
/// success branch renders with zero rows).
/// </summary>
public abstract class AnalyzePageTestBase : BunitContext
{
    protected static readonly DateTime FixedNowUtc = new(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);

    protected Mock<IProviderAuthService> AuthServiceMock { get; } = new();
    protected Mock<IDeviceRepository> DeviceRepositoryMock { get; } = new();
    protected Mock<IReportGenerationService> ReportGenerationServiceMock { get; } = new();
    protected Mock<IJammingRepository> JammingRepositoryMock { get; } = new();
    protected Mock<IDeviceHealthRepository> DeviceHealthRepositoryMock { get; } = new();

    protected ScopeState ScopeState { get; }

    protected AnalyzePageTestBase()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        Services.AddLocalization();
        Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();

        AuthServiceMock
            .Setup(m => m.IsAuthenticatedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        DeviceRepositoryMock
            .Setup(m => m.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Device>());

        ReportGenerationServiceMock
            .Setup(m => m.BuildForensicAnalysisReportAsync(It.IsAny<Guid?>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ForensicAnalysisReport());
        ReportGenerationServiceMock
            .Setup(m => m.BuildSignalAnomalyReportAsync(It.IsAny<Guid?>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SignalAnomalyReport());
        ReportGenerationServiceMock
            .Setup(m => m.BuildAccessControlReportAsync(It.IsAny<Guid?>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AccessControlReport());

        DeviceHealthRepositoryMock
            .Setup(m => m.GetHistoryAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<DeviceHealth>());
        JammingRepositoryMock
            .Setup(m => m.RecomputeStatsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new JammingStatsSummary());
        JammingRepositoryMock
            .Setup(m => m.GetStatsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((JammingStatsSummary?)null);
        JammingRepositoryMock
            .Setup(m => m.ListIncidentsAsync(It.IsAny<Guid?>(), It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<JammingIncidentRecord>());

        ScopeState = new ScopeState(new FakeTimeProvider(FixedNowUtc));

        Services.AddScoped(_ => AuthServiceMock.Object);
        Services.AddScoped(_ => DeviceRepositoryMock.Object);
        Services.AddScoped(_ => ReportGenerationServiceMock.Object);
        Services.AddScoped(_ => ScopeState);
        Services.AddScoped(_ => new JammingToolsOrchestrator(
            Mock.Of<ILogger<JammingToolsOrchestrator>>(),
            JammingRepositoryMock.Object,
            DeviceHealthRepositoryMock.Object));
    }

    protected static Device CreateTestDevice(string name = "Test Device") => new()
    {
        Id = Guid.NewGuid(),
        LocationId = Guid.NewGuid(),
        ProviderDeviceId = Guid.NewGuid().ToString(),
        Name = name,
        Type = "camera"
    };

    protected NavigationManager Nav => Services.GetRequiredService<NavigationManager>();

    /// <summary>Navigates to /analyze?analysis=&lt;key&gt; before the page is rendered, simulating a deep link.</summary>
    protected void NavigateToAnalysis(string key) => Nav.NavigateTo($"/analyze?analysis={key}", replace: true);

    protected IRenderedComponent<Analyze> RenderPage() => Render<Analyze>();
}

public class Analyze_DefaultTab_Tests : AnalyzePageTestBase
{
    [Fact]
    public void DefaultLoad_ReportsTabActive_QueriesAllDevices()
    {
        var component = RenderPage();

        var reportsButtonClass = component.Find("[data-testid='analysis-reports']").GetAttribute("class");
        Assert.Contains("active", reportsButtonClass);

        var anomaliesButtonClass = component.Find("[data-testid='analysis-anomalies']").GetAttribute("class");
        Assert.DoesNotContain("active", anomaliesButtonClass);

        ReportGenerationServiceMock.Verify(
            m => m.BuildForensicAnalysisReportAsync(null, ScopeState.Current.FromUtc, ScopeState.Current.ToUtc, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void NotAuthenticated_ShowsSignInPrompt_NoServiceCalls()
    {
        AuthServiceMock
            .Setup(m => m.IsAuthenticatedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var component = RenderPage();

        Assert.Contains("not signed in", component.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(component.FindAll("[data-testid='analysis-reports']"));
        ReportGenerationServiceMock.Verify(
            m => m.BuildForensicAnalysisReportAsync(It.IsAny<Guid?>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}

/// <summary>Scenario 2: single-device vs multi-device scoping, for each of the three
/// device-tolerant panels (Reports/Anomalies/Access).</summary>
public class Analyze_DeviceScoping_Tests : AnalyzePageTestBase
{
    [Fact]
    public void OneDeviceScope_Reports_CallsWithThatDeviceId()
    {
        var device = CreateTestDevice();
        ScopeState.Set(ScopeState.Current with { DeviceIds = new List<Guid> { device.Id } });

        RenderPage();

        ReportGenerationServiceMock.Verify(
            m => m.BuildForensicAnalysisReportAsync(device.Id, ScopeState.Current.FromUtc, ScopeState.Current.ToUtc, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void TwoDeviceScope_Reports_ShowsNotice_NoServiceCall()
    {
        ScopeState.Set(ScopeState.Current with { DeviceIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() } });

        var component = RenderPage();

        Assert.Contains("Select a single device (or all)", component.Markup);
        ReportGenerationServiceMock.Verify(
            m => m.BuildForensicAnalysisReportAsync(It.IsAny<Guid?>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void OneDeviceScope_Anomalies_CallsWithThatDeviceId()
    {
        var device = CreateTestDevice();
        ScopeState.Set(ScopeState.Current with { DeviceIds = new List<Guid> { device.Id } });
        NavigateToAnalysis("anomalies");

        RenderPage();

        ReportGenerationServiceMock.Verify(
            m => m.BuildSignalAnomalyReportAsync(device.Id, ScopeState.Current.FromUtc, ScopeState.Current.ToUtc, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void TwoDeviceScope_Anomalies_ShowsNotice_NoServiceCall()
    {
        ScopeState.Set(ScopeState.Current with { DeviceIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() } });
        NavigateToAnalysis("anomalies");

        var component = RenderPage();

        Assert.Contains("Select a single device (or all)", component.Markup);
        ReportGenerationServiceMock.Verify(
            m => m.BuildSignalAnomalyReportAsync(It.IsAny<Guid?>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void OneDeviceScope_Access_CallsWithThatDeviceId()
    {
        var device = CreateTestDevice();
        ScopeState.Set(ScopeState.Current with { DeviceIds = new List<Guid> { device.Id } });
        NavigateToAnalysis("access");

        RenderPage();

        ReportGenerationServiceMock.Verify(
            m => m.BuildAccessControlReportAsync(device.Id, ScopeState.Current.FromUtc, ScopeState.Current.ToUtc, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void TwoDeviceScope_Access_ShowsNotice_NoServiceCall()
    {
        ScopeState.Set(ScopeState.Current with { DeviceIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() } });
        NavigateToAnalysis("access");

        var component = RenderPage();

        Assert.Contains("Select a single device (or all)", component.Markup);
        ReportGenerationServiceMock.Verify(
            m => m.BuildAccessControlReportAsync(It.IsAny<Guid?>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}

/// <summary>Scenario 3: clicking a tab button updates the URL (preserving unrelated keys) and
/// queries that tab's service.</summary>
public class Analyze_TabSwitch_Tests : AnalyzePageTestBase
{
    [Fact]
    public void ClickAnomaliesTab_UpdatesUrl_PreservesOtherKeys_QueriesAnomaliesService()
    {
        var deviceId = Guid.NewGuid();
        Nav.NavigateTo($"/analyze?case=CASE-1&from=2026-01-01&devices={deviceId:D}&analysis=reports", replace: true);

        var component = RenderPage();
        ReportGenerationServiceMock.Invocations.Clear();

        component.Find("[data-testid='analysis-anomalies']").Click();

        Assert.Contains("analysis=anomalies", Nav.Uri);
        Assert.Contains("case=CASE-1", Nav.Uri);
        Assert.Contains("from=2026-01-01", Nav.Uri);
        Assert.Contains($"devices={deviceId:D}", Nav.Uri);

        // Bug repro guard: the old ScopeUrl.Merge misuse duplicated non-scope keys and left a
        // stale second "analysis=" pair in the URL (see Analyze.razor.SelectAnalysis comment).
        Assert.DoesNotContain("analysis=reports", Nav.Uri);
        Assert.Equal(1, CountOccurrences(Nav.Uri, "case="));
        Assert.Equal(1, CountOccurrences(Nav.Uri, "analysis="));

        var anomaliesButtonClass = component.Find("[data-testid='analysis-anomalies']").GetAttribute("class");
        Assert.Contains("active", anomaliesButtonClass);

        ReportGenerationServiceMock.Verify(
            m => m.BuildSignalAnomalyReportAsync(null, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        int count = 0;
        int index = 0;
        while ((index = haystack.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }
}

/// <summary>Scenario 4: ScopeState window changes re-query only the active panel; switching tabs
/// doesn't leave the previous panel re-querying in the background.</summary>
public class Analyze_ScopeChange_Tests : AnalyzePageTestBase
{
    [Fact]
    public async Task ScopeWindowChange_ActivePanelRequeriesWithNewDates()
    {
        var component = RenderPage();
        ReportGenerationServiceMock.Invocations.Clear();

        var newFrom = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var newTo = new DateTime(2020, 2, 1, 0, 0, 0, DateTimeKind.Utc);

        await component.InvokeAsync(() => ScopeState.Set(ScopeState.Current with { FromUtc = newFrom, ToUtc = newTo }));

        ReportGenerationServiceMock.Verify(
            m => m.BuildForensicAnalysisReportAsync(null, newFrom, newTo, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SwitchingTabs_DoesNotLeavePreviousPanelRequeryingOnScopeChange()
    {
        var component = RenderPage();
        ReportGenerationServiceMock.Verify(
            m => m.BuildForensicAnalysisReportAsync(It.IsAny<Guid?>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once);

        component.Find("[data-testid='analysis-anomalies']").Click();
        ReportGenerationServiceMock.Verify(
            m => m.BuildSignalAnomalyReportAsync(It.IsAny<Guid?>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once);

        // Switching to anomalies must not have re-invoked the reports build method again.
        ReportGenerationServiceMock.Verify(
            m => m.BuildForensicAnalysisReportAsync(It.IsAny<Guid?>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once);

        // Now on the anomalies tab: changing scope must query anomalies again, but must NOT
        // re-trigger the (no longer shown) reports panel.
        await component.InvokeAsync(() => ScopeState.Set(ScopeState.Current with { FromUtc = ScopeState.Current.FromUtc.AddDays(-1) }));

        ReportGenerationServiceMock.Verify(
            m => m.BuildSignalAnomalyReportAsync(It.IsAny<Guid?>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        ReportGenerationServiceMock.Verify(
            m => m.BuildForensicAnalysisReportAsync(It.IsAny<Guid?>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}

/// <summary>Scenario 5: the Jamming panel, deep-linked directly.</summary>
public class Analyze_JammingDeepLink_Tests : AnalyzePageTestBase
{
    [Fact]
    public void OneDevice_JammingPanelShown_OrchestratorInvokedForThatDevice()
    {
        var device = CreateTestDevice();
        ScopeState.Set(ScopeState.Current with { DeviceIds = new List<Guid> { device.Id } });
        NavigateToAnalysis("jamming");

        var component = RenderPage();

        DeviceHealthRepositoryMock.Verify(m => m.GetHistoryAsync(device.Id, It.IsAny<CancellationToken>()), Times.Once);
        JammingRepositoryMock.Verify(m => m.RecomputeStatsAsync(device.Id, It.IsAny<CancellationToken>()), Times.Once);
        Assert.Empty(component.FindAll(".alert-danger"));
    }

    [Fact]
    public void ZeroDevices_ShowsSelectSingleDeviceMessage_NoOrchestratorCall()
    {
        NavigateToAnalysis("jamming");

        var component = RenderPage();

        Assert.Contains("Select a single device for jamming analysis", component.Markup);
        DeviceHealthRepositoryMock.Verify(m => m.GetHistoryAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

/// <summary>Scenario 7: a report-generation failure shows an error, and the tab bar keeps working.</summary>
public class Analyze_ErrorHandling_Tests : AnalyzePageTestBase
{
    [Fact]
    public void ReportsServiceThrows_ShowsError_TabSwitchStillWorks()
    {
        ReportGenerationServiceMock
            .Setup(m => m.BuildForensicAnalysisReportAsync(It.IsAny<Guid?>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("report store unreachable"));

        var component = RenderPage();

        Assert.Contains("report store unreachable", component.Markup);

        component.Find("[data-testid='analysis-anomalies']").Click();

        Assert.DoesNotContain("report store unreachable", component.Markup);
        ReportGenerationServiceMock.Verify(
            m => m.BuildSignalAnomalyReportAsync(It.IsAny<Guid?>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once);

        var anomaliesButtonClass = component.Find("[data-testid='analysis-anomalies']").GetAttribute("class");
        Assert.Contains("active", anomaliesButtonClass);
    }
}

/// <summary>Scenario 6: the four legacy routes redirect to /analyze?analysis=&lt;key&gt;, preserving
/// any other query string the old URL carried.</summary>
public class AnalyzeRedirectPages_Tests : BunitContext
{
    public AnalyzeRedirectPages_Tests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private NavigationManager Nav => Services.GetRequiredService<NavigationManager>();

    [Fact]
    public void ForensicReports_RedirectsToAnalyzeReports_PreservesQuery()
    {
        Nav.NavigateTo("/analyze/reports?from=2026-09-01", replace: true);

        Render<ForensicReports>();

        Assert.Contains("/analyze?analysis=reports", Nav.Uri);
        Assert.Contains("from=2026-09-01", Nav.Uri);
    }

    [Fact]
    public void SignalAnomalies_RedirectsToAnalyzeAnomalies_PreservesQuery()
    {
        Nav.NavigateTo("/analyze/signal-anomalies?from=2026-09-01", replace: true);

        Render<SignalAnomalies>();

        Assert.Contains("/analyze?analysis=anomalies", Nav.Uri);
        Assert.Contains("from=2026-09-01", Nav.Uri);
    }

    [Fact]
    public void AccessControl_RedirectsToAnalyzeAccess_PreservesQuery()
    {
        Nav.NavigateTo("/analyze/access-control?from=2026-09-01", replace: true);

        Render<AccessControl>();

        Assert.Contains("/analyze?analysis=access", Nav.Uri);
        Assert.Contains("from=2026-09-01", Nav.Uri);
    }

    [Fact]
    public void JammingAnalysis_RedirectsToAnalyzeJamming_PreservesQuery()
    {
        Nav.NavigateTo("/analyze/jamming?from=2026-09-01", replace: true);

        Render<JammingAnalysis>();

        Assert.Contains("/analyze?analysis=jamming", Nav.Uri);
        Assert.Contains("from=2026-09-01", Nav.Uri);
    }
}
