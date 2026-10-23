namespace VideoForensics.Ui.Shared.Tests;

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using Syncfusion.Blazor.DropDowns;
using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Ui.Shared.Contracts;
using VideoForensics.Ui.Shared.Pages;
using VideoForensics.Ui.Shared.Resources;
using VideoForensics.Ui.Shared.Components.Inspector;
using VideoForensics.Ui.Shared.Services;
using VideoForensics.Ui.Shared.Services.Inspector;
using VideoForensics.Ui.Shared.Services.QueryApi;
using VideoForensics.Ui.Shared.Services.Scope;
using Xunit;

public class QueryApiDevicesPage_Tests : BunitContext
{
    private readonly Mock<IDeviceDiscoveryService> _discovery = new();

    public QueryApiDevicesPage_Tests()
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
        Services.AddScoped<RightPanelContentService>();
        Services.AddSingleton(_discovery.Object);
        Services.AddSingleton(new Mock<IEventAndConfigService>().Object);
        Services.AddLogging();
        _discovery.Setup(d => d.GetLocationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Location>());
        SetRendererInfo(new RendererInfo("Server", true));
    }

    [Fact]
    public void QueryApiDevicesPage_Breadcrumb_ShowsSourcesQueryApiThenDevicesAsCurrentPage()
    {
        var cut = Render<QueryApiDevices>();

        var items = cut.FindAll("nav ol > li");
        Assert.Equal(3, items.Count);
        Assert.Equal("/accounts", items[0].QuerySelector("a")?.GetAttribute("href"));
        Assert.Equal("/query", items[1].QuerySelector("a")?.GetAttribute("href"));
        Assert.Equal("Query API", items[1].TextContent.Trim());
        Assert.Equal("page", items[2].GetAttribute("aria-current"));
        Assert.Equal("Devices", items[2].TextContent.Trim());
    }

    [Fact]
    public void QueryApiDevicesPage_LocationDropdown_ListsLoadedLocationsByNameWithIdValue()
    {
        _discovery.Setup(d => d.GetLocationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Location>
            {
                new("loc-1", "Home", "1 Main St"),
                new("loc-2", "Office", null),
            });

        Render<QueryApiDevices>();
        var panel = Render(GetRightPanelContent());
        var dropdown = panel.FindComponents<SfDropDownList<string, QueryApiSelectOption>>()
            .Single(c => c.Instance.ID == "query-api-location-filter");
        var options = dropdown.Instance.DataSource!.ToList();

        Assert.Equal(new[] { "Home", "Office" }, options.Select(o => o.Label));
        Assert.Equal(new[] { "loc-1", "loc-2" }, options.Select(o => o.Value));
    }

    [Fact]
    public void QueryApiDevicesPage_LocationDropdown_ShowsSharedLocationId()
    {
        Services.GetRequiredService<IQueryApiFilterState>().SetLocationId("loc-2");

        Render<QueryApiDevices>();
        var panel = Render(GetRightPanelContent());
        var dropdown = panel.FindComponents<SfDropDownList<string, QueryApiSelectOption>>()
            .Single(c => c.Instance.ID == "query-api-location-filter");

        Assert.Equal("loc-2", dropdown.Instance.Value);
    }

    [Fact]
    public async Task QueryApiDevicesPage_DeviceRowSelected_DoesNotShowInspector()
    {
        var device = new Device("dev-1", "Front Door", "doorbell", "loc-1", true);
        _discovery.Setup(d => d.GetDevicesAsync("loc-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Device> { device });
        Services.GetRequiredService<IQueryApiFilterState>().SetLocationId("loc-1");

        var cut = Render<QueryApiDevices>();
        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Get Devices").Click();
        var grid = cut.FindComponent<ForensicGrid<Device>>();
        await cut.InvokeAsync(() => grid.Instance.HandleRowSelectedAsync(device));

        Assert.Null(Services.GetRequiredService<InspectorState>().Current);
    }

    [Fact]
    public async Task QueryApiDevicesPage_DeviceRowSelected_NavigatesToDeviceDetailPage()
    {
        var device = new Device("dev-1", "Front Door", "doorbell", "loc-1", true);
        _discovery.Setup(d => d.GetDevicesAsync("loc-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Device> { device });
        Services.GetRequiredService<IQueryApiFilterState>().SetLocationId("loc-1");

        var cut = Render<QueryApiDevices>();
        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Get Devices").Click();
        var grid = cut.FindComponent<ForensicGrid<Device>>();
        await cut.InvokeAsync(() => grid.Instance.HandleRowSelectedAsync(device));

        Assert.EndsWith("/query/devices/loc-1/dev-1", Services.GetRequiredService<NavigationManager>().Uri);
        Assert.Equal("dev-1", Services.GetRequiredService<IQueryApiFilterState>().DeviceId);
    }

    private RenderFragment GetRightPanelContent()
    {
        var content = Services.GetRequiredService<RightPanelContentService>().Content;
        Assert.NotNull(content);
        return content;
    }
}
