namespace VideoForensics.Ui.Shared.Tests;

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Moq;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Ui.Shared.Pages;
using Xunit;

/// <summary>Render tests for /devices/config/ring/{id}: typed Ring settings, and the cases where the page does not apply.</summary>
public class RingDeviceSettingsPage_Tests : BunitContext
{
    private static readonly Guid RingDeviceId = Guid.Parse("e1a2a7c5-eec0-4663-a351-d72add1e30ad");

    private readonly Mock<IDeviceRepository> _devices = new();
    private readonly Mock<IDeviceTypedSettingsReader> _typed = new();

    public RingDeviceSettingsPage_Tests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddLocalization();
        Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();
        Services.AddScoped(_ => _devices.Object);
        Services.AddScoped(_ => _typed.Object);
        SetRendererInfo(new Microsoft.AspNetCore.Components.RendererInfo("Server", true));
    }

    private static Device RingDevice(Guid id, string? settingsJson = null) => new()
    {
        Id = id,
        LocationId = Guid.NewGuid(),
        ProviderDeviceId = "1",
        Name = "Front Door",
        Type = "doorbot",
        ProviderName = "Ring",
        SettingsMetadataJson = settingsJson,
    };

    private IRenderedComponent<RingDeviceSettings> RenderPage(Guid id) =>
        Render<RingDeviceSettings>(parameters => parameters.Add(p => p.DeviceId, id));

    [Fact]
    public void RingDeviceSettingsPage_RingDeviceWithTypedRows_ShowsOfflineDetectionTypesThresholdsAndOtherSettings()
    {
        var device = RingDevice(RingDeviceId, "{\"live_view_disabled\":false}");
        _devices.Setup(d => d.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Device> { device });
        _typed.Setup(t => t.GetAsync(RingDeviceId, It.IsAny<CancellationToken>())).ReturnsAsync(new DeviceTypedSettings(
            new DeviceOfflineMotionSettings { DeviceId = RingDeviceId, MaxUploadKb = 5000, PeriodAfterSecs = 30 },
            [new DeviceDetectionType { DeviceId = RingDeviceId, DetectionType = "human", Enabled = true, Mode = "cloud", Record = false, Notification = true }],
            [new DeviceDetectionThreshold { DeviceId = RingDeviceId, DetectionType = "loitering", Threshold = 10.0 }]));

        var cut = RenderPage(RingDeviceId);

        cut.WaitForAssertion(() => Assert.Contains("Front Door", cut.Markup));
        Assert.Contains("5000", cut.Find("[data-testid=ring-offline-motion]").TextContent);
        Assert.Contains("human", cut.Find("[data-testid=ring-detection-types]").TextContent);
        Assert.Contains("loitering", cut.Find("[data-testid=ring-thresholds]").TextContent);
        Assert.Contains("live_view_disabled", cut.Find("[data-testid=ring-other-settings]").TextContent);
    }

    [Fact]
    public void RingDeviceSettingsPage_RingDeviceWithNoTypedRows_ShowsNoTypedSettingsMessage()
    {
        _devices.Setup(d => d.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Device> { RingDevice(RingDeviceId) });
        _typed.Setup(t => t.GetAsync(RingDeviceId, It.IsAny<CancellationToken>())).ReturnsAsync(new DeviceTypedSettings(null, [], []));

        var cut = RenderPage(RingDeviceId);

        cut.WaitForAssertion(() => Assert.Contains("No typed settings are stored for this device.", cut.Markup));
        Assert.Empty(cut.FindAll("[data-testid=ring-offline-motion]"));
    }

    [Fact]
    public void RingDeviceSettingsPage_NonRingDevice_ShowsNotRingMessage()
    {
        var wyze = RingDevice(RingDeviceId);
        wyze.ProviderName = "Wyze";
        _devices.Setup(d => d.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Device> { wyze });

        var cut = RenderPage(RingDeviceId);

        cut.WaitForAssertion(() => Assert.Contains("This device is not linked to a Ring account", cut.Markup));
        _typed.Verify(t => t.GetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void RingDeviceSettingsPage_UnknownDevice_ShowsNotFoundMessage()
    {
        _devices.Setup(d => d.ListAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Device>());

        var cut = RenderPage(Guid.NewGuid());

        cut.WaitForAssertion(() => Assert.Contains("This device was not found.", cut.Markup));
    }
}
