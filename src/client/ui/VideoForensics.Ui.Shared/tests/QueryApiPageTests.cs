
using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

using Moq;

using System.Reactive.Subjects;

using VideoForensics.Core.Api.Contracts;
using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Client.Ui.Shared.Services.SelfTest;
using VideoForensics.Client.Ui.Shared.Components.Inspector;
using VideoForensics.Client.Ui.Shared.Contracts;
using VideoForensics.Client.Ui.Shared.Pages;
using VideoForensics.Client.Ui.Shared.Resources;
using VideoForensics.Client.Ui.Shared.Services.QueryApi;
using VideoForensics.Client.Ui.Shared.Services.Scope;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

public class QueryApiPage_Tests : BunitContext
{
    private readonly Mock<IDeviceDiscoveryService> _discovery = new();
    private readonly Mock<IProviderAuthService> _auth = new();

    public QueryApiPage_Tests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        _ = Services.AddLocalization();
        _ = Services.AddSingleton<IStringLocalizer<SharedResources>>(TestLocalizer.Create());
        _ = Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        _ = Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        _ = Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();
        _ = Services.AddSingleton(new ScopeState());
        _ = Services.AddScoped<IQueryApiFilterState, QueryApiFilterState>();
        _ = Services.AddScoped<VideoForensics.Client.Ui.Shared.Services.Inspector.InspectorState>();
        _ = Services.AddScoped<VideoForensics.Client.Ui.Shared.Services.RightPanelContentService>();
        _ = Services.AddSingleton(_auth.Object);
        _ = Services.AddSingleton(_discovery.Object);
        _ = Services.AddSingleton(new Mock<IEventAndConfigService>().Object);
        _ = Services.AddLogging();

        // The API Tester is embedded in this page, so its services must resolve too. No runs are started here.
        Mock<IRingSelfTestService> selfTest = new();
        _ = selfTest.Setup(m => m.ListEndpointsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<SelfTestEndpointDto>());
        _ = selfTest.Setup(m => m.GetStatusAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new SelfTestStatusDto(SelfTestRunStatus.Idle));
        _ = Services.AddScoped(_ => selfTest.Object);
        Mock<ISelfTestStatusSource> statusSource = new();
        _ = statusSource.SetupGet(m => m.Status).Returns(new Subject<SelfTestStatusDto>());
        _ = Services.AddScoped(_ => statusSource.Object);
        _ = Services.AddScoped(sp => new VideoForensics.Client.Ui.Shared.Services.PairedSessionState(sp.GetRequiredService<Microsoft.JSInterop.IJSRuntime>()));
        _ = Services.AddScoped(_ => new VideoForensics.Client.Ui.Shared.Services.Inspector.InspectorState());
        SetRendererInfo(new RendererInfo("Server", true));
    }

    [Fact]
    public void QueryApiPage_Breadcrumb_ShowsSourcesThenQueryApiAsCurrentPage()
    {
        IRenderedComponent<QueryApi> cut = Render<QueryApi>();

        IReadOnlyList<AngleSharp.Dom.IElement> items = cut.FindAll("nav ol > li");
        Assert.Equal(2, items.Count);
        AngleSharp.Dom.IElement? sourcesLink = items[0].QuerySelector("a");
        Assert.NotNull(sourcesLink);
        Assert.Equal("/accounts", sourcesLink.GetAttribute("href"));
        Assert.Equal("Sources", sourcesLink.TextContent.Trim());
        Assert.Equal("page", items[1].GetAttribute("aria-current"));
        Assert.Equal("Query API", items[1].TextContent.Trim());
    }

    [Fact]
    public void QueryApiPage_CheckStatus_ShowsFormattedStatusAndRawPayloadWithoutSecrets()
    {
        _ = _auth.Setup(a => a.GetAuthStatus()).Returns("Authenticated");
        _ = _auth.Setup(a => a.GetAuthStatusDetailsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
            new ProviderAuthStatus(true, "Bearer", new DateTime(2026, 10, 24, 12, 0, 0, DateTimeKind.Utc), "client"));
        IRenderedComponent<QueryApi> cut = Render<QueryApi>();

        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Check Status").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Authenticated", cut.Markup);
            Assert.Contains("Raw response", cut.Markup);
            Assert.Contains("Bearer", cut.Markup);
            Assert.Contains("2026-10-24", cut.Markup);
            Assert.DoesNotContain("accessToken", cut.Markup, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void QueryApiPage_CheckStatus_ProviderWithoutDetails_SaysSoInsteadOfShowingPayload()
    {
        _ = _auth.Setup(a => a.GetAuthStatus()).Returns("Authenticated");
        _ = _auth.Setup(a => a.GetAuthStatusDetailsAsync(It.IsAny<CancellationToken>())).ReturnsAsync((ProviderAuthStatus?)null);
        IRenderedComponent<QueryApi> cut = Render<QueryApi>();

        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Check Status").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("does not report token details", cut.Markup);
            Assert.DoesNotContain("Raw response", cut.Markup);
        });
    }

    [Fact]
    public void QueryApiPage_EmbedsApiTesterSection()
    {
        IRenderedComponent<QueryApi> cut = Render<QueryApi>();

        AngleSharp.Dom.IElement section = cut.Find("[data-testid='api-tester']");
        Assert.NotNull(section);
        Assert.Empty(cut.FindAll("a").Where(a => a.GetAttribute("href") == "/query/api-tester"));
    }

    [Fact]
    public void QueryApiPage_SubpageButtons_LinkToDevicesEventsAndConfig()
    {
        IRenderedComponent<QueryApi> cut = Render<QueryApi>();

        var hrefs = cut.FindAll("a").Select(a => (a.TextContent.Trim(), a.GetAttribute("href"))).ToList();
        Assert.Contains(("Devices", "/query/devices"), hrefs);
        Assert.Contains(("Events", "/query/events"), hrefs);
        Assert.Contains(("Config", "/query/config"), hrefs);
    }

    [Fact]
    public async Task QueryApiPage_LocationRowSelected_SetsSharedLocationId()
    {
        _ = _discovery.Setup(d => d.GetLocationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new("loc-1", "Home", "1 Main St"),
                new("loc-2", "Office", null),
            ]);

        IRenderedComponent<QueryApi> cut = Render<QueryApi>();
        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Get Locations").Click();
        cut.WaitForState(() => cut.FindComponents<ForensicGrid<Location>>().Count == 1);

        IRenderedComponent<ForensicGrid<Location>> grid = cut.FindComponent<ForensicGrid<Location>>();
        await cut.InvokeAsync(() => grid.Instance.OnRowSelected.InvokeAsync(new Location("loc-2", "Office", null)));

        Assert.Equal("loc-2", Services.GetRequiredService<IQueryApiFilterState>().LocationId);
    }
}
