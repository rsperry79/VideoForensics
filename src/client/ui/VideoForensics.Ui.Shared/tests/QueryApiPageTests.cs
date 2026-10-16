namespace VideoForensics.Ui.Shared.Tests;

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Ui.Shared.Components.Inspector;
using VideoForensics.Ui.Shared.Contracts;
using VideoForensics.Ui.Shared.Services;
using VideoForensics.Ui.Shared.Pages;
using VideoForensics.Ui.Shared.Resources;
using VideoForensics.Ui.Shared.Services.Scope;
using VideoForensics.Ui.Shared.Services.QueryApi;
using Xunit;

public class QueryApiPage_Tests : BunitContext
{
    private readonly Mock<IDeviceDiscoveryService> _discovery = new();

    public QueryApiPage_Tests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        Services.AddLocalization();
        Services.AddSingleton<IStringLocalizer<SharedResources>>(TestLocalizer.Create());
        Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();
        Services.AddSingleton(new ScopeState());
        Services.AddScoped<IQueryApiFilterState, QueryApiFilterState>();
        Services.AddScoped<VideoForensics.Ui.Shared.Services.Inspector.InspectorState>();
        Services.AddScoped<VideoForensics.Ui.Shared.Services.RightPanelContentService>();
        Services.AddSingleton(new Mock<IProviderAuthService>().Object);
        Services.AddSingleton(_discovery.Object);
        Services.AddSingleton(new Mock<IEventAndConfigService>().Object);
        Services.AddLogging();
        SetRendererInfo(new RendererInfo("Server", true));
    }

    [Fact]
    public void QueryApiPage_Breadcrumb_ShowsSourcesThenQueryApiAsCurrentPage()
    {
        var cut = Render<QueryApi>();

        var items = cut.FindAll("nav ol > li");
        Assert.Equal(2, items.Count);
        var sourcesLink = items[0].QuerySelector("a");
        Assert.NotNull(sourcesLink);
        Assert.Equal("/accounts", sourcesLink.GetAttribute("href"));
        Assert.Equal("Sources", sourcesLink.TextContent.Trim());
        Assert.Equal("page", items[1].GetAttribute("aria-current"));
        Assert.Equal("Query API", items[1].TextContent.Trim());
    }

    [Fact]
    public void QueryApiPage_ApiTesterButton_LinksToApiTester()
    {
        var cut = Render<QueryApi>();

        var button = cut.FindAll("a").Single(a => a.TextContent.Trim() == "API Tester");
        Assert.Equal("/query/api-tester", button.GetAttribute("href"));
    }

    [Fact]
    public void QueryApiPage_SubpageButtons_LinkToDevicesEventsAndConfig()
    {
        var cut = Render<QueryApi>();

        var hrefs = cut.FindAll("a").Select(a => (a.TextContent.Trim(), a.GetAttribute("href"))).ToList();
        Assert.Contains(("Devices", "/query/devices"), hrefs);
        Assert.Contains(("Events", "/query/events"), hrefs);
        Assert.Contains(("Config", "/query/config"), hrefs);
    }

    [Fact]
    public async Task QueryApiPage_LocationRowSelected_SetsSharedLocationId()
    {
        _discovery.Setup(d => d.GetLocationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Location>
            {
                new("loc-1", "Home", "1 Main St"),
                new("loc-2", "Office", null),
            });

        var cut = Render<QueryApi>();
        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Get Locations").Click();
        cut.WaitForState(() => cut.FindComponents<ForensicGrid<Location>>().Count == 1);

        var grid = cut.FindComponent<ForensicGrid<Location>>();
        await cut.InvokeAsync(() => grid.Instance.OnRowSelected.InvokeAsync(new Location("loc-2", "Office", null)));

        Assert.Equal("loc-2", Services.GetRequiredService<IQueryApiFilterState>().LocationId);
    }
}
