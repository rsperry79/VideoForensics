
using AngleSharp.Dom;

using Bunit;
using Bunit.Rendering;

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

namespace VideoForensics.Ui.Shared.Tests;

public class QueryApiEventsPage_Tests : BunitContext
{
    private readonly Mock<IDeviceDiscoveryService> _discovery = new();

    public QueryApiEventsPage_Tests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        _ = Services.AddLocalization();
        _ = Services.AddSingleton<IStringLocalizer<SharedResources>>(TestLocalizer.Create());
        _ = Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        _ = Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        _ = Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();
        _ = Services.AddSingleton(new ScopeState());
        _ = Services.AddScoped<IQueryApiFilterState, QueryApiFilterState>();
        _ = Services.AddScoped<VideoForensics.Ui.Shared.Services.Inspector.InspectorState>();
        _ = Services.AddScoped<RightPanelContentService>();
        _ = Services.AddSingleton(_discovery.Object);
        _ = Services.AddSingleton(new Mock<IEventAndConfigService>().Object);
        _ = Services.AddLogging();
        _ = _discovery.Setup(d => d.GetLocationsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        SetRendererInfo(new RendererInfo("Server", true));
    }

    [Fact]
    public void QueryApiEventsPage_Breadcrumb_ShowsSourcesQueryApiThenDeviceEventsAsCurrentPage()
    {
        IRenderedComponent<QueryApiEvents> cut = Render<QueryApiEvents>();

        IReadOnlyList<IElement> items = cut.FindAll("nav ol > li");
        Assert.Equal(3, items.Count);
        Assert.Equal("/accounts", items[0].QuerySelector("a")?.GetAttribute("href"));
        Assert.Equal("/query", items[1].QuerySelector("a")?.GetAttribute("href"));
        Assert.Equal("page", items[2].GetAttribute("aria-current"));
        Assert.Equal("Device Events", items[2].TextContent.Trim());
    }

    [Fact]
    public async Task QueryApiEventsPage_LocationSelectedOnDevicesPage_IsLocationDropdownValueOnEventsPage()
    {
        IRenderedComponent<QueryApiDevices> devicesPage = Render<QueryApiDevices>();
        IRenderedComponent<ContainerFragment> devicesPanel = Render(GetRightPanelContent());
        IRenderedComponent<SfDropDownList<string, QueryApiSelectOption>> devicesDropdown = devicesPanel.FindComponents<SfDropDownList<string, QueryApiSelectOption>>()
            .Single(c => c.Instance.ID == "query-api-location-filter");
        await devicesPage.InvokeAsync(() => devicesDropdown.Instance.ValueChanged.InvokeAsync("loc-2"));

        _ = Render<QueryApiEvents>();
        IRenderedComponent<ContainerFragment> eventsPanel = Render(GetRightPanelContent());
        IRenderedComponent<SfDropDownList<string, QueryApiSelectOption>> eventsDropdown = eventsPanel.FindComponents<SfDropDownList<string, QueryApiSelectOption>>()
            .Single(c => c.Instance.ID == "query-api-events-location-filter");

        Assert.Equal("loc-2", eventsDropdown.Instance.Value);
    }

    [Fact]
    public void QueryApiEventsPage_DeviceDropdown_ListsLoadedDevicesByNameWithIdValue()
    {
        Services.GetRequiredService<IQueryApiFilterState>().SetLocationId("loc-1");
        _ = _discovery.Setup(d => d.GetDevicesAsync("loc-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new("dev-1", "Lobby", "camera", "loc-1", true),
                new("dev-2", "Garage", "camera", "loc-1", false),
            ]);

        _ = Render<QueryApiEvents>();
        IRenderedComponent<ContainerFragment> panel = Render(GetRightPanelContent());
        IRenderedComponent<SfDropDownList<string, QueryApiSelectOption>> dropdown = panel.FindComponents<SfDropDownList<string, QueryApiSelectOption>>()
            .Single(c => c.Instance.ID == "query-api-device-events-filter");
        var options = dropdown.Instance.DataSource!.ToList();

        Assert.Equal(new[] { "Lobby", "Garage" }, options.Select(o => o.Label));
        Assert.Equal(new[] { "dev-1", "dev-2" }, options.Select(o => o.Value));
    }

    private RenderFragment GetRightPanelContent()
    {
        RenderFragment? content = Services.GetRequiredService<RightPanelContentService>().Content;
        Assert.NotNull(content);
        return content;
    }
}
