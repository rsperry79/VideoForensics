namespace VideoForensics.Ui.Shared.Tests;

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Moq;
using Xunit;
using VideoForensics.Api.Contracts;
using VideoForensics.Ui.Shared.Components.Inspector;
using VideoForensics.Ui.Shared.Pages;
using VideoForensics.Ui.Shared.Services.Inspector;

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

        protected override Task OnInitializedAsync() => Task.CompletedTask;

        protected override Task OnParametersSetAsync() => Task.CompletedTask;

        protected override Task OnAfterRenderAsync(bool firstRender) => Task.CompletedTask;
    }

    protected readonly Mock<IRingSelfTestService> SelfTestServiceMock = new();
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
        SchemaIssues: new List<string>(),
        HttpCalls: new List<SelfTestHttpCallDto> { TestPhaseCall, RestorePhaseCall });

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
        SchemaIssues: new List<string>(),
        HttpCalls: new List<SelfTestHttpCallDto>());

    protected RingSelfTestPageTestBase()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();
        Services.AddScoped(_ => SelfTestServiceMock.Object);
        Services.AddScoped(_ => InspectorState);
        Services.AddLocalization();
        Services.AddScoped<VideoForensics.Ui.Shared.Services.RightPanelContentService>();

        ComponentFactories.Add<Syncfusion.Blazor.Grids.SfGrid<SelfTestCallDto>, TestSfGrid>();
        ComponentFactories.AddStub<Syncfusion.Blazor.Grids.GridColumn>();
        ComponentFactories.AddStub<Syncfusion.Blazor.Grids.GridPageSettings>();
        ComponentFactories.AddStub<Syncfusion.Blazor.Grids.GridEvents<SelfTestCallDto>>();

        SelfTestServiceMock
            .Setup(m => m.ListEndpointsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SelfTestEndpointDto>
            {
                new("call_a", "Call A", "desc", "oauth2", "GET", "/a", "None", false, false)
            });
    }

    /// <summary>
    /// Arranges the service so that on initial load the run status already reports Completed,
    /// with the given result available — simulating an operator reopening the tester after a
    /// run has already finished on the server, without ever clicking "Run".
    /// </summary>
    protected void SetupCompletedOnLoad(SelfTestResultDto result)
    {
        SelfTestServiceMock
            .Setup(m => m.GetStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SelfTestStatusDto(SelfTestRunStatus.Completed, DateTime.UtcNow.AddSeconds(-5), DateTime.UtcNow));

        SelfTestServiceMock
            .Setup(m => m.GetResultAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
    }

    protected static SelfTestResultDto MakeResult(params SelfTestCallDto[] calls) => new(
        ToolVersion: "1.0.0",
        GeneratedAtUtc: DateTime.UtcNow,
        CredentialSource: "Database",
        Summary: new SelfTestSummaryDto(calls.Length, calls.Length, 0),
        Calls: calls);

    protected IRenderedComponent<RingSelfTest> RenderPage() => Render<RingSelfTest>();
}

public class RingSelfTestPage_InitialLoad_Tests : RingSelfTestPageTestBase
{
    [Fact]
    public void OnInitialized_LoadsEndpoints()
    {
        SelfTestServiceMock
            .Setup(m => m.GetStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SelfTestStatusDto(SelfTestRunStatus.Idle));

        RenderPage();

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

        var component = RenderPage();

        SelfTestServiceMock.Verify(m => m.GetResultAsync(It.IsAny<CancellationToken>()), Times.Once);
        SelfTestServiceMock.Verify(m => m.StartRunAsync(It.IsAny<SelfTestRunRequestDto>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Contains("Results", component.Markup);

        var grid = component.FindComponent<ForensicGrid<SelfTestCallDto>>();
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
        var component = RenderPage();
        var grid = component.FindComponent<ForensicGrid<SelfTestCallDto>>();

        await component.InvokeAsync(() => grid.Instance.HandleRowSelectedAsync(CallA));

        var section = component.Find("[data-testid='raw-calls']");
        var sectionMarkup = section.InnerHtml;

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
        var rawTextPre = component.Find("pre.raw-text");
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
        var component = RenderPage();
        var grid = component.FindComponent<ForensicGrid<SelfTestCallDto>>();

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
        SelfTestServiceMock
            .Setup(m => m.GetStatusAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SelfTestStatusDto(SelfTestRunStatus.Failed, DateTime.UtcNow.AddSeconds(-5), DateTime.UtcNow, "boom-error"));

        var component = RenderPage();

        Assert.Contains("Run Failed", component.Markup);
        Assert.Contains("boom-error", component.Markup);
        Assert.Empty(component.FindComponents<ForensicGrid<SelfTestCallDto>>());
        Assert.Empty(component.FindAll("[data-testid='raw-calls']"));

        SelfTestServiceMock.Verify(m => m.GetResultAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
