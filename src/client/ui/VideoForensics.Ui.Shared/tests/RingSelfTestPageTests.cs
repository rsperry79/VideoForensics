
using Bunit;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

using Moq;

using Syncfusion.Blazor.DropDowns;

using System.Reactive.Subjects;

using VideoForensics.Core.Api.Contracts;
using VideoForensics.Client.Ui.Shared.Components.Inspector;
using VideoForensics.Client.Ui.Shared.Contracts;
using VideoForensics.Client.Ui.Shared.Pages;
using VideoForensics.Client.Ui.Shared.Services;
using VideoForensics.Client.Ui.Shared.Services.Inspector;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;
/// <summary>
/// Shared scenario setup for RingSelfTest page tests. Call A carries two HTTP calls (a JSON
/// "test"-phase call and a truncated, non-JSON "restore"-phase call); call B carries none.
/// </summary>
public abstract class RingSelfTestPageTestBase : BunitContext
{
    /// <summary>
    /// A real, minimal subclass of <see cref="Syncfusion.Blazor.Grids.SfGrid{TValue}"/> used as a
    /// bUnit component double, the same trick <c>ForensicGridTests</c> and the Evidence/Cases page
    /// tests use: bUnit's generic stub isn't assignable to the typed <c>@ref</c> capture inside
    /// <see cref="ForensicGrid{TItem}"/>, but a real subclass is. It renders nothing itself, so row
    /// content is never present in markup; tests drive selection directly through
    /// <see cref="ForensicGrid{TItem}.HandleRowSelectedAsync"/> instead.
    /// </summary>
    private sealed class TestSfGrid : Syncfusion.Blazor.Grids.SfGrid<SelfTestCallDto>
    {
        protected override void BuildRenderTree(Microsoft.AspNetCore.Components.Rendering.RenderTreeBuilder builder)
        {
        }

        protected override Task OnInitializedAsync()
        {
            return Task.CompletedTask;
        }

        protected override Task OnParametersSetAsync()
        {
            return Task.CompletedTask;
        }

        protected override Task OnAfterRenderAsync(bool firstRender)
        {
            return Task.CompletedTask;
        }
    }

    protected readonly Mock<IRingSelfTestService> SelfTestServiceMock = new();
    protected readonly Subject<SelfTestStatusDto> StatusStream = new();
    protected readonly Mock<ISelfTestStatusSource> StatusSourceMock = new();
    protected readonly InspectorState InspectorState = new();

    protected static readonly SelfTestHttpCallDto TestPhaseCall = new(
        Method: "GET",
        Url: "https://api.example.com/a/test-endpoint",
        StatusCode: 200,
        Phase: "test",
        TimestampUtc: new DateTime(2026, 9, 25, 10, 15, 30, DateTimeKind.Utc),
        ResponseBodyBytes: 42,
        Body: "{\"outcome\":\"succeeded-value-xyz\"}",
        BodyTruncated: false);

    protected static readonly SelfTestHttpCallDto RestorePhaseCall = new(
        Method: "POST",
        Url: "https://api.example.com/a/restore-endpoint",
        StatusCode: 200,
        Phase: "restore",
        TimestampUtc: new DateTime(2026, 9, 25, 10, 15, 31, DateTimeKind.Utc),
        ResponseBodyBytes: 500000,
        Body: "RESTORE_PHASE_PLAIN_BODY_TEXT_NOT_JSON_9f3a",
        BodyTruncated: true);

    protected static readonly SelfTestCallDto CallA = new(
        Endpoint: "call_a",
        DisplayName: "Call A",
        SessionMethod: "oauth2",
        Destructive: false,
        Physical: false,
        Target: "device-a",
        StartedAtUtc: DateTime.UtcNow,
        DurationMs: 150,
        Success: true,
        Error: null,
        RestoreAttempted: true,
        RestoreSuccess: true,
        RestoreError: null,
        RestoreSkippedReason: null,
        SchemaIssues: [],
        HttpCalls: [TestPhaseCall, RestorePhaseCall]);

    protected static readonly SelfTestCallDto CallB = new(
        Endpoint: "call_b",
        DisplayName: "Call B",
        SessionMethod: "oauth2",
        Destructive: false,
        Physical: false,
        Target: null,
        StartedAtUtc: DateTime.UtcNow,
        DurationMs: 80,
        Success: true,
        Error: null,
        RestoreAttempted: false,
        RestoreSuccess: null,
        RestoreError: null,
        RestoreSkippedReason: null,
        SchemaIssues: [],
        HttpCalls: []);

    protected RingSelfTestPageTestBase()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        _ = Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        _ = Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        _ = Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();
        _ = Services.AddScoped(_ => SelfTestServiceMock.Object);
        _ = StatusSourceMock.SetupGet(m => m.Status).Returns(StatusStream);
        _ = Services.AddScoped(_ => StatusSourceMock.Object);
        _ = Services.AddLogging();
        _ = Services.AddScoped(_ => InspectorState);
        _ = Services.AddLocalization();
        _ = Services.AddScoped<RightPanelContentService>();
        // The page checks the operator's tier before offering destructive endpoints. These tests exercise those
        // endpoints, so they run as a SuperAdmin on the Local tier.
        _ = Services.AddScoped(sp => new PairedSessionState(sp.GetRequiredService<Microsoft.JSInterop.IJSRuntime>()));
        PairedSessionState session = Services.GetRequiredService<PairedSessionState>();
        session.SetAsync("test-token", Guid.NewGuid(), "SuperAdmin").GetAwaiter().GetResult();
        session.SetServerSessionKind(localTier: true, passkeySession: false);

        _ = ComponentFactories.Add<Syncfusion.Blazor.Grids.SfGrid<SelfTestCallDto>, TestSfGrid>();
        _ = ComponentFactories.AddStub<Syncfusion.Blazor.Grids.GridColumn>();
        _ = ComponentFactories.AddStub<Syncfusion.Blazor.Grids.GridPageSettings>();
        _ = ComponentFactories.AddStub<Syncfusion.Blazor.Grids.GridEvents<SelfTestCallDto>>();

        _ = SelfTestServiceMock
            .Setup(m => m.ListEndpointsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new("call_a", "Call A", "desc", "oauth2", "GET", "/a", "None", false, false, "Ring")
            ]);
    }

    /// <summary>
    /// Arranges the service so that on initial load the run status already reports Completed,
    /// with the given result available — simulating an operator reopening the tester after a
    /// run has already finished on the server, without ever clicking "Run".
    /// </summary>
    protected void SetupCompletedOnLoad(SelfTestResultDto result)
    {
        _ = SelfTestServiceMock
            .Setup(m => m.GetStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SelfTestStatusDto(SelfTestRunStatus.Completed, DateTime.UtcNow.AddSeconds(-5), DateTime.UtcNow));

        _ = SelfTestServiceMock
            .Setup(m => m.GetResultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
    }

    protected static SelfTestResultDto MakeResult(params SelfTestCallDto[] calls)
    {
        return new(
        ToolVersion: "1.0.0",
        GeneratedAtUtc: DateTime.UtcNow,
        CredentialSource: "Database",
        Summary: new SelfTestSummaryDto(calls.Length, calls.Length, 0),
        Calls: calls);
    }

    protected IRenderedComponent<RingSelfTest> RenderPage()
    {
        return Render<RingSelfTest>();
    }
}

public class RingSelfTestPage_InitialLoad_Tests : RingSelfTestPageTestBase
{
    [Fact]
    public void OnInitialized_LoadsEndpoints()
    {
        _ = SelfTestServiceMock
            .Setup(m => m.GetStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SelfTestStatusDto(SelfTestRunStatus.Idle));

        _ = RenderPage();

        SelfTestServiceMock.Verify(m => m.ListEndpointsAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// The page must show a completed run's results as soon as it opens — an operator reopening
    /// the self-test tester should see the last run without having to click "Run" again. This
    /// exercises the existing OnInitializedAsync branch that fetches the result when the server
    /// already reports Completed.
    /// </summary>
    [Fact]
    public void PageOpen_WithCompletedResultAlreadyOnServer_ShowsResultsWithoutRunningAgain()
    {
        SetupCompletedOnLoad(MakeResult(CallA, CallB));

        IRenderedComponent<RingSelfTest> component = RenderPage();

        SelfTestServiceMock.Verify(m => m.GetResultAsync(It.IsAny<CancellationToken>()), Times.Once);
        SelfTestServiceMock.Verify(m => m.StartRunAsync(It.IsAny<SelfTestRunRequestDto>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Contains("Results", component.Markup);

        IRenderedComponent<ForensicGrid<SelfTestCallDto>> grid = component.FindComponent<ForensicGrid<SelfTestCallDto>>();
        var displayNames = grid.Instance.DataSource!.Select(c => c.DisplayName).ToList();
        Assert.Contains(CallA.DisplayName, displayNames);
        Assert.Contains(CallB.DisplayName, displayNames);
    }
}

public class RingSelfTestPage_RowSelection_Tests : RingSelfTestPageTestBase
{
    [Fact]
    public async Task SelectCallWithHttpCalls_ShowsBothCallsInOrderWithBodiesAndTruncationNote()
    {
        SetupCompletedOnLoad(MakeResult(CallA, CallB));
        IRenderedComponent<RingSelfTest> component = RenderPage();
        IRenderedComponent<ForensicGrid<SelfTestCallDto>> grid = component.FindComponent<ForensicGrid<SelfTestCallDto>>();

        await component.InvokeAsync(() => grid.Instance.HandleRowSelectedAsync(CallA));

        AngleSharp.Dom.IElement section = component.Find("[data-testid='raw-calls']");
        string sectionMarkup = section.InnerHtml;

        // Both HTTP call headers are present, and appear in call order (test, then restore).
        Assert.Contains(TestPhaseCall.Url, sectionMarkup);
        Assert.Contains(RestorePhaseCall.Url, sectionMarkup);
        Assert.True(
            sectionMarkup.IndexOf(TestPhaseCall.Url, StringComparison.Ordinal) <
            sectionMarkup.IndexOf(RestorePhaseCall.Url, StringComparison.Ordinal),
            "Expected the test-phase call to be listed before the restore-phase call.");
        Assert.Contains("TEST", sectionMarkup);
        Assert.Contains("RESTORE", sectionMarkup);
        Assert.Contains("GET", sectionMarkup);
        Assert.Contains("POST", sectionMarkup);

        // The JSON body renders through DumpView, exposing a value from the body.
        Assert.Contains("succeeded-value-xyz", sectionMarkup);

        // The non-JSON body renders as raw text in a <pre class="raw-text">.
        AngleSharp.Dom.IElement rawTextPre = component.Find("pre.raw-text");
        Assert.Contains(RestorePhaseCall.Body, rawTextPre.TextContent);

        // The truncation note reports the actual response size.
        Assert.Contains("500000", sectionMarkup);
        Assert.Contains("Truncated", sectionMarkup);

        // Inspector state reflects the selected call.
        Assert.NotNull(InspectorState.Current);
        Assert.Equal(TestPhaseCall.Body, InspectorState.Current!.RawJson);
        Assert.NotNull(InspectorState.Current.Provenance);
        Assert.Equal(2, InspectorState.Current.Provenance!.Count);
    }

    [Fact]
    public async Task SelectCallWithoutHttpCalls_ShowsNoCallsMessageAndClearsPriorSection()
    {
        SetupCompletedOnLoad(MakeResult(CallA, CallB));
        IRenderedComponent<RingSelfTest> component = RenderPage();
        IRenderedComponent<ForensicGrid<SelfTestCallDto>> grid = component.FindComponent<ForensicGrid<SelfTestCallDto>>();

        // Select A first so there is a previously-rendered raw-calls section to replace.
        await component.InvokeAsync(() => grid.Instance.HandleRowSelectedAsync(CallA));
        Assert.Contains(TestPhaseCall.Url, component.Markup);

        await component.InvokeAsync(() => grid.Instance.HandleRowSelectedAsync(CallB));

        Assert.Contains("No HTTP calls recorded for this endpoint.", component.Markup);

        // Call A's content must be gone, not merely appended to.
        Assert.DoesNotContain(TestPhaseCall.Url, component.Markup);
        Assert.DoesNotContain(RestorePhaseCall.Url, component.Markup);
        Assert.DoesNotContain("succeeded-value-xyz", component.Markup);
        Assert.Empty(component.FindAll("pre.raw-text"));
    }
}

public class RingSelfTestPage_FailedRun_Tests : RingSelfTestPageTestBase
{
    [Fact]
    public void PageOpen_WithFailedStatus_ShowsErrorAndNoGrid()
    {
        _ = SelfTestServiceMock
            .Setup(m => m.GetStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SelfTestStatusDto(SelfTestRunStatus.Failed, DateTime.UtcNow.AddSeconds(-5), DateTime.UtcNow, "boom-error"));

        IRenderedComponent<RingSelfTest> component = RenderPage();

        Assert.Contains("Run Failed", component.Markup);
        Assert.Contains("boom-error", component.Markup);
        Assert.Empty(component.FindComponents<ForensicGrid<SelfTestCallDto>>());
        Assert.Empty(component.FindAll("[data-testid='raw-calls']"));

        SelfTestServiceMock.Verify(m => m.GetResultAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}

/// <summary>
/// The page is driven by <see cref="ISelfTestStatusSource"/>, not a timer. A run's result is fetched exactly once,
/// on the transition to Completed, and a replayed or repeated Completed for the same run must not fetch it again.
/// </summary>
public class RingSelfTestPage_StatusStream_Tests : RingSelfTestPageTestBase
{
    private static readonly DateTime RunStarted = new(2026, 10, 9, 10, 0, 0, DateTimeKind.Utc);

    private static SelfTestStatusDto CompletedRun(DateTime started)
    {
        return new(SelfTestRunStatus.Completed, started, started.AddSeconds(5));
    }

    private void ArrangeOnLoad(SelfTestStatusDto onLoad)
    {
        _ = SelfTestServiceMock
            .Setup(m => m.GetStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(onLoad);

        _ = SelfTestServiceMock
            .Setup(m => m.GetResultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeResult(CallA));
    }

    [Fact]
    public async Task RingSelfTestPage_StatusAfterDispose_DoesNotFetchResult()
    {
        ArrangeOnLoad(new SelfTestStatusDto(SelfTestRunStatus.Idle));
        IRenderedComponent<RingSelfTest> component = RenderPage();
        await component.InvokeAsync(() => ((IAsyncDisposable)component.Instance).DisposeAsync().AsTask());

        Exception? ex = Record.Exception(() => StatusStream.OnNext(CompletedRun(RunStarted)));
        await Task.Delay(50);

        Assert.Null(ex);
        SelfTestServiceMock.Verify(m => m.GetResultAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void RingSelfTestPage_CompletedFromStream_FetchesResultOnce()
    {
        ArrangeOnLoad(new SelfTestStatusDto(SelfTestRunStatus.Idle));
        IRenderedComponent<RingSelfTest> component = RenderPage();

        StatusStream.OnNext(new SelfTestStatusDto(SelfTestRunStatus.Running, RunStarted));
        StatusStream.OnNext(CompletedRun(RunStarted));

        component.WaitForAssertion(() =>
            SelfTestServiceMock.Verify(m => m.GetResultAsync(It.IsAny<CancellationToken>()), Times.Once));
    }

    [Fact]
    public async Task RingSelfTestPage_RepeatedCompletedForSameRun_DoesNotRefetchResult()
    {
        ArrangeOnLoad(new SelfTestStatusDto(SelfTestRunStatus.Idle));
        IRenderedComponent<RingSelfTest> component = RenderPage();

        StatusStream.OnNext(CompletedRun(RunStarted));
        StatusStream.OnNext(CompletedRun(RunStarted));
        StatusStream.OnNext(CompletedRun(RunStarted));
        await component.InvokeAsync(() => { });

        SelfTestServiceMock.Verify(m => m.GetResultAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RingSelfTestPage_CompletedOnLoadThenSameRunReplayed_FetchesResultOnce()
    {
        // Initial one-shot status already reports the run as Completed, then the stream replays that same run.
        ArrangeOnLoad(CompletedRun(RunStarted));
        IRenderedComponent<RingSelfTest> component = RenderPage();

        StatusStream.OnNext(CompletedRun(RunStarted));
        await component.InvokeAsync(() => { });

        SelfTestServiceMock.Verify(m => m.GetResultAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RingSelfTestPage_NewRunCompletes_FetchesResultAgain()
    {
        ArrangeOnLoad(new SelfTestStatusDto(SelfTestRunStatus.Idle));
        IRenderedComponent<RingSelfTest> component = RenderPage();

        StatusStream.OnNext(CompletedRun(RunStarted));
        StatusStream.OnNext(CompletedRun(RunStarted.AddMinutes(1)));
        await component.InvokeAsync(() => { });

        SelfTestServiceMock.Verify(m => m.GetResultAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task RingSelfTestPage_RunningFromStream_DoesNotFetchResult()
    {
        ArrangeOnLoad(new SelfTestStatusDto(SelfTestRunStatus.Idle));
        IRenderedComponent<RingSelfTest> component = RenderPage();

        StatusStream.OnNext(new SelfTestStatusDto(SelfTestRunStatus.Running, RunStarted));
        await component.InvokeAsync(() => { });

        SelfTestServiceMock.Verify(m => m.GetResultAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RingSelfTestPage_RunningOnLoad_DoesNotPollStatus()
    {
        // Before the change the page re-read status every 500 ms while a run was in progress.
        ArrangeOnLoad(new SelfTestStatusDto(SelfTestRunStatus.Running, RunStarted));
        _ = RenderPage();

        await Task.Delay(1500);

        SelfTestServiceMock.Verify(m => m.GetStatusAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}

public class RingSelfTestPage_Routing_Tests : RingSelfTestPageTestBase
{
    [Fact]
    public void RingSelfTest_Routes_IncludesLegacyAndQueryApiSubpage()
    {
        var templates = typeof(RingSelfTest)
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Components.RouteAttribute), inherit: false)
            .Cast<Microsoft.AspNetCore.Components.RouteAttribute>()
            .Select(r => r.Template)
            .ToList();

        Assert.Contains("/tools/ring-selftest", templates);
        Assert.Contains("/query/api-tester", templates);
    }

    [Fact]
    public void RingSelfTestPage_Breadcrumb_LinksBackToQueryApi()
    {
        _ = SelfTestServiceMock
            .Setup(m => m.GetStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SelfTestStatusDto(SelfTestRunStatus.Idle));

        IRenderedComponent<RingSelfTest> component = RenderPage();

        AngleSharp.Dom.IElement queryLink = component.FindAll("nav a").Single(a => a.GetAttribute("href") == "/query");
        Assert.Equal("Query API", queryLink.TextContent.Trim());
    }
}

public class RingSelfTestPage_Filter_Tests : RingSelfTestPageTestBase
{
    private static SelfTestEndpointDto Ep(string key, string provider, bool destructive = false)
    {
        return new(key, key, "d", "oauth2", "GET", "/p", "None", destructive, false, provider);
    }

    public RingSelfTestPage_Filter_Tests()
    {
        _ = SelfTestServiceMock
            .Setup(m => m.GetStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SelfTestStatusDto(SelfTestRunStatus.Idle));
        _ = SelfTestServiceMock
            .Setup(m => m.ListEndpointsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                Ep("ring_a", "Ring"), Ep("ring_b", "Ring"), Ep("wyze_a", "Wyze"),
                Ep("ring_d", "Ring", destructive: true),
            ]);
    }

    /// <summary>
    /// The controls live in the right-panel slot, which is a separate render root. Rendering the slot afresh
    /// reflects the page's current state, so each assertion calls this after the interaction it checks.
    /// </summary>
    private IRenderedComponent<Microsoft.AspNetCore.Components.IComponent> Panel(IRenderedComponent<RingSelfTest> page)
    {
        RightPanelContentService slot = Services.GetRequiredService<RightPanelContentService>();
        Assert.NotNull(slot.Content);
        return Render(slot.Content!);
    }

    private static List<string> Keys(IRenderedComponent<Syncfusion.Blazor.DropDowns.SfMultiSelect<string[], SelfTestEndpointDto>> c)
    {
        return c.Instance.DataSource!.Select(e => e.Key).ToList();
    }

    private IReadOnlyList<IRenderedComponent<Syncfusion.Blazor.DropDowns.SfMultiSelect<string[], SelfTestEndpointDto>>> Groups(IRenderedComponent<Microsoft.AspNetCore.Components.IComponent> page)
    {
        return page.FindComponents<Syncfusion.Blazor.DropDowns.SfMultiSelect<string[], SelfTestEndpointDto>>();
    }

    [Fact]
    public void RingSelfTestPage_ProviderFilter_NarrowsEndpoints()
    {
        IRenderedComponent<RingSelfTest> page = RenderPage();

        Panel(page).Find("[data-testid='provider-filter']").Change("Wyze");

        IRenderedComponent<SfMultiSelect<string[], SelfTestEndpointDto>> readOnly = Groups(Panel(page)).Single();
        Assert.Equal(["wyze_a"], Keys(readOnly));
    }

    [Fact]
    public void RingSelfTestPage_ProviderFilter_ShowsHumanReadableLabelsOnly()
    {
        IRenderedComponent<RingSelfTest> page = RenderPage();

        IReadOnlyList<AngleSharp.Dom.IElement> options = Panel(page).FindAll("[data-testid='provider-filter'] option");

        Assert.Contains(options, o => o.TextContent.Trim() == "Wyze");
        Assert.Contains(options, o => o.TextContent.Trim() == "Ring");
        Assert.DoesNotContain(options, o => o.TextContent.Contains("Provider_"));
    }

    [Fact]
    public void RingSelfTestPage_CategoryFilter_OffersHumanReadableLabels()
    {
        IRenderedComponent<RingSelfTest> page = RenderPage();

        var texts = Panel(page).FindAll("[data-testid='category-filter'] option").Select(o => o.TextContent.Trim()).ToList();

        Assert.Contains("Read-only", texts);
        Assert.Contains("Destructive / physical", texts);
        Assert.DoesNotContain("ReadOnly", texts);
    }

    [Fact]
    public void RingSelfTestPage_CategoryFilter_ExcludesCategoryWithNoEndpointsForProvider()
    {
        IRenderedComponent<RingSelfTest> page = RenderPage();

        Panel(page).Find("[data-testid='provider-filter']").Change("Wyze");

        var texts = Panel(page).FindAll("[data-testid='category-filter'] option").Select(o => o.TextContent.Trim()).ToList();
        Assert.DoesNotContain("Destructive / physical", texts);
    }

    [Fact]
    public void RingSelfTestPage_ReadOnlySelectAll_SelectsOnlyReadOnlyGroup()
    {
        IRenderedComponent<RingSelfTest> page = RenderPage();

        Panel(page).Find("[data-testid='readonly-select-all']").Click();

        IReadOnlyList<IRenderedComponent<SfMultiSelect<string[], SelfTestEndpointDto>>> groups = Groups(Panel(page));
        Assert.Equal(["ring_a", "ring_b", "wyze_a"], groups[0].Instance.Value!.OrderBy(k => k, StringComparer.Ordinal));
        Assert.Empty(groups[1].Instance.Value ?? []);
    }

    [Fact]
    public void RingSelfTestPage_ReadOnlyUnselectAll_ClearsOnlyReadOnlyGroup()
    {
        IRenderedComponent<RingSelfTest> page = RenderPage();
        Panel(page).Find("[data-testid='destructive-select-all']").Click();
        Panel(page).Find("[data-testid='readonly-select-all']").Click();

        Panel(page).Find("[data-testid='readonly-unselect-all']").Click();

        IReadOnlyList<IRenderedComponent<SfMultiSelect<string[], SelfTestEndpointDto>>> groups = Groups(Panel(page));
        Assert.Empty(groups[0].Instance.Value ?? []);
        Assert.Equal(["ring_d"], groups[1].Instance.Value);
    }
}
