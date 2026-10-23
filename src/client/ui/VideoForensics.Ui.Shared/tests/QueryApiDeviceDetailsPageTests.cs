
using AngleSharp.Dom;

using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

using Moq;

using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Ui.Shared.Contracts;
using VideoForensics.Ui.Shared.Pages;
using VideoForensics.Ui.Shared.Resources;
using VideoForensics.Ui.Shared.Services.QueryApi;
using VideoForensics.Ui.Shared.Services.Scope;

using Xunit;

namespace VideoForensics.Ui.Shared.Tests;

public class QueryApiDeviceDetailsPage_Tests : BunitContext
{
    private readonly Mock<IDeviceDiscoveryService> _discovery = new();
    private readonly Mock<IDeviceControlService> _control = new();

    public QueryApiDeviceDetailsPage_Tests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        _ = Services.AddLocalization();
        _ = Services.AddSingleton<IStringLocalizer<SharedResources>>(TestLocalizer.Create());
        _ = Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        _ = Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        _ = Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();
        _ = Services.AddSingleton(new ScopeState());
        _ = Services.AddScoped<IQueryApiFilterState, QueryApiFilterState>();
        _ = Services.AddSingleton(_discovery.Object);
        _ = Services.AddSingleton(_control.Object);
        _ = Services.AddLogging();
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
        _ = _discovery.Setup(d => d.GetDevicesAsync("loc-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(devices.ToList());
    }

    [Fact]
    public void QueryApiDeviceDetailsPage_Breadcrumb_ShowsSourcesQueryApiDevicesThenDeviceNameAsCurrentPage()
    {
        SetupDevices(new Device("dev-1", "Front Door", "doorbell", "loc-1", true));

        IRenderedComponent<QueryApiDeviceDetails> cut = RenderDetails("loc-1", "dev-1");

        IReadOnlyList<IElement> items = cut.FindAll("nav ol > li");
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

        IRenderedComponent<QueryApiDeviceDetails> cut = RenderDetails("loc-1", "dev-1");

        Assert.Equal("Front Door", cut.Find("[data-testid='device-name']").TextContent.Trim());
        Assert.Equal("Yes", cut.Find("[data-testid='device-status']").TextContent.Trim());
        Assert.Empty(cut.FindAll("table"));
    }

    [Fact]
    public void QueryApiDeviceDetailsPage_UnknownDeviceId_ShowsNotFoundMessage()
    {
        SetupDevices(new Device("dev-1", "Front Door", "doorbell", "loc-1", true));

        IRenderedComponent<QueryApiDeviceDetails> cut = RenderDetails("loc-1", "dev-missing");

        Assert.Equal("Device not found in this location.", cut.Find("[data-testid='device-not-found']").TextContent.Trim());
    }

    [Fact]
    public void DeviceControls_DoorbotType_ShowsLightAndMotionButtonsNotDnd()
    {
        SetupDevices(new Device("dev-1", "Front Door", "doorbot", "loc-1", true));

        IRenderedComponent<QueryApiDeviceDetails> cut = RenderDetails("loc-1", "dev-1");

        _ = Assert.Single(cut.FindAll("[data-testid='control-light-on']"));
        _ = Assert.Single(cut.FindAll("[data-testid='control-motion-on']"));
        Assert.Empty(cut.FindAll("[data-testid='control-dnd-apply']"));
    }

    [Fact]
    public void DeviceControls_ChimeType_ShowsDndControlNotLight()
    {
        SetupDevices(new Device("chime-1", "Hall Chime", "chime", "loc-1", true));

        IRenderedComponent<QueryApiDeviceDetails> cut = RenderDetails("loc-1", "chime-1");

        _ = Assert.Single(cut.FindAll("[data-testid='control-dnd-apply']"));
        Assert.Empty(cut.FindAll("[data-testid='control-light-on']"));
    }

    [Fact]
    public void DeviceControls_LightOnClicked_CallsSetLightOnceWithProviderId()
    {
        SetupDevices(new Device("dev-1", "Front Door", "doorbot", "loc-1", true));
        IRenderedComponent<QueryApiDeviceDetails> cut = RenderDetails("loc-1", "dev-1");

        cut.Find("[data-testid='control-light-on']").Click();

        _control.Verify(c => c.SetLightAsync("dev-1", true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void DeviceControls_ControlFails_ShowsLocalizedFailureNotExceptionText()
    {
        SetupDevices(new Device("dev-1", "Front Door", "doorbot", "loc-1", true));
        _ = _control.Setup(c => c.SetLightAsync("dev-1", true, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("POST https://api.ring.com/devices/dev-1 failed"));
        IRenderedComponent<QueryApiDeviceDetails> cut = RenderDetails("loc-1", "dev-1");

        cut.Find("[data-testid='control-light-on']").Click();

        cut.WaitForAssertion(() => Assert.Equal(
            "The device did not accept the command. Try again.",
            cut.Find("[data-testid='control-message']").TextContent.Trim()));
        Assert.DoesNotContain("api.ring.com", cut.Markup);
    }

    [Fact]
    public void DeviceControls_VolumeOutOfRange_ShowsInvalidRangeAndCallsNothing()
    {
        SetupDevices(new Device("dev-1", "Front Door", "doorbot", "loc-1", true));
        IRenderedComponent<QueryApiDeviceDetails> cut = RenderDetails("loc-1", "dev-1");

        cut.Find("[data-testid='control-volume-input']").Change("12");
        cut.Find("[data-testid='control-volume-apply']").Click();

        Assert.Equal("Enter a value in the allowed range.", cut.Find("[data-testid='control-message']").TextContent.Trim());
        _control.Verify(c => c.SetVolumeAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
