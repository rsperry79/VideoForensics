namespace VideoForensics.Ui.Shared.Tests;

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Ui.Shared.Contracts;
using VideoForensics.Ui.Shared.Pages;
using VideoForensics.Ui.Shared.Resources;
using VideoForensics.Ui.Shared.Services;
using VideoForensics.Ui.Shared.Services.QueryApi;
using VideoForensics.Ui.Shared.Services.Scope;
using Xunit;

public class QueryApiDeviceDetailsPage_Tests : BunitContext
{
    private readonly Mock<IDeviceDiscoveryService> _discovery = new();

    public QueryApiDeviceDetailsPage_Tests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        Services.AddLocalization();
        Services.AddSingleton<IStringLocalizer<SharedResources>>(TestLocalizer.Create());
        Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();
        Services.AddSingleton(new ScopeState());
        Services.AddScoped<IQueryApiFilterState, QueryApiFilterState>();
        Services.AddSingleton(_discovery.Object);
        Services.AddLogging();
        SetRendererInfo(new RendererInfo("Server", true));
    }

    private IRenderedComponent<QueryApiDeviceDetails> RenderDetails(string locationId, string deviceId)
    {
        return Render<QueryApiDeviceDetails>(p => p
            .Add(x => x.LocationId, locationId)
            .Add(x => x.DeviceId, deviceId));
    }

    private void SetupDevices(params Device[] devices)
    {
        _discovery.Setup(d => d.GetDevicesAsync("loc-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(devices.ToList());
    }

    [Fact]
    public void QueryApiDeviceDetailsPage_Breadcrumb_ShowsSourcesQueryApiDevicesThenDeviceNameAsCurrentPage()
    {
        SetupDevices(new Device("dev-1", "Front Door", "doorbell", "loc-1", true));

        var cut = RenderDetails("loc-1", "dev-1");

        var items = cut.FindAll("nav ol > li");
        Assert.Equal(4, items.Count);
        Assert.Equal("/accounts", items[0].QuerySelector("a")?.GetAttribute("href"));
        Assert.Equal("/query", items[1].QuerySelector("a")?.GetAttribute("href"));
        Assert.Equal("Query API", items[1].TextContent.Trim());
        Assert.Equal("/query/devices", items[2].QuerySelector("a")?.GetAttribute("href"));
        Assert.Equal("Devices", items[2].TextContent.Trim());
        Assert.Equal("page", items[3].GetAttribute("aria-current"));
        Assert.Equal("Front Door", items[3].TextContent.Trim());
    }

    [Fact]
    public void QueryApiDeviceDetailsPage_DeviceLoaded_RendersNameAndOnlineStatus()
    {
        SetupDevices(new Device("dev-1", "Front Door", "doorbell", "loc-1", true));

        var cut = RenderDetails("loc-1", "dev-1");

        Assert.Equal("Front Door", cut.Find("[data-testid='device-name']").TextContent.Trim());
        Assert.Equal("Yes", cut.Find("[data-testid='device-status']").TextContent.Trim());
        Assert.Empty(cut.FindAll("table"));
    }

    [Fact]
    public void QueryApiDeviceDetailsPage_UnknownDeviceId_ShowsNotFoundMessage()
    {
        SetupDevices(new Device("dev-1", "Front Door", "doorbell", "loc-1", true));

        var cut = RenderDetails("loc-1", "dev-missing");

        Assert.Equal("Device not found in this location.", cut.Find("[data-testid='device-not-found']").TextContent.Trim());
    }
}
