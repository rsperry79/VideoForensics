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
using VideoForensics.Ui.Shared.Services;
using VideoForensics.Ui.Shared.Services.QueryApi;
using VideoForensics.Ui.Shared.Services.Scope;
using Xunit;

public class QueryApiEventsPage_Tests : BunitContext
{
    private readonly Mock<IDeviceDiscoveryService> _discovery = new();

    public QueryApiEventsPage_Tests()
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
    public void QueryApiEventsPage_Breadcrumb_ShowsSourcesQueryApiThenDeviceEventsAsCurrentPage()
    {
        var cut = Render<QueryApiEvents>();

        var items = cut.FindAll("nav ol > li");
        Assert.Equal(3, items.Count);
        Assert.Equal("/accounts", items[0].QuerySelector("a")?.GetAttribute("href"));
        Assert.Equal("/query", items[1].QuerySelector("a")?.GetAttribute("href"));
        Assert.Equal("page", items[2].GetAttribute("aria-current"));
        Assert.Equal("Device Events", items[2].TextContent.Trim());
    }

    [Fact]
    public async Task QueryApiEventsPage_LocationSelectedOnDevicesPage_IsLocationDropdownValueOnEventsPage()
    {
        var devicesPage = Render<QueryApiDevices>();
        var devicesPanel = Render(GetRightPanelContent());
        var devicesDropdown = devicesPanel.FindComponents<SfDropDownList<string, QueryApiSelectOption>>()
            .Single(c => c.Instance.ID == "query-api-location-filter");
        await devicesPage.InvokeAsync(() => devicesDropdown.Instance.ValueChanged.InvokeAsync("loc-2"));

        Render<QueryApiEvents>();
        var eventsPanel = Render(GetRightPanelContent());
        var eventsDropdown = eventsPanel.FindComponents<SfDropDownList<string, QueryApiSelectOption>>()
            .Single(c => c.Instance.ID == "query-api-events-location-filter");

        Assert.Equal("loc-2", eventsDropdown.Instance.Value);
    }

    [Fact]
    public void QueryApiEventsPage_DeviceDropdown_ListsLoadedDevicesByNameWithIdValue()
    {
        Services.GetRequiredService<IQueryApiFilterState>().SetLocationId("loc-1");
        _discovery.Setup(d => d.GetDevicesAsync("loc-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Device>
            {
                new("dev-1", "Lobby", "camera", "loc-1", true),
                new("dev-2", "Garage", "camera", "loc-1", false),
            });

        Render<QueryApiEvents>();
        var panel = Render(GetRightPanelContent());
        var dropdown = panel.FindComponents<SfDropDownList<string, QueryApiSelectOption>>()
            .Single(c => c.Instance.ID == "query-api-device-events-filter");
        var options = dropdown.Instance.DataSource!.ToList();

        Assert.Equal(new[] { "Lobby", "Garage" }, options.Select(o => o.Label));
        Assert.Equal(new[] { "dev-1", "dev-2" }, options.Select(o => o.Value));
    }

    private RenderFragment GetRightPanelContent()
    {
        var content = Services.GetRequiredService<RightPanelContentService>().Content;
        Assert.NotNull(content);
        return content;
    }
}
