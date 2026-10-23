
using AngleSharp.Dom;

using Bunit;

using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

using Moq;

using Syncfusion.Blazor.Inputs;

using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Ui.Shared.Contracts;
using VideoForensics.Ui.Shared.Pages;
using VideoForensics.Ui.Shared.Resources;
using VideoForensics.Ui.Shared.Services;
using VideoForensics.Ui.Shared.Services.QueryApi;
using VideoForensics.Ui.Shared.Services.Scope;

using Xunit;

namespace VideoForensics.Ui.Shared.Tests;

public class QueryApiConfigPage_Tests : BunitContext
{
    public QueryApiConfigPage_Tests()
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
        _ = Services.AddSingleton(new Mock<IDeviceDiscoveryService>().Object);
        _ = Services.AddSingleton(new Mock<IEventAndConfigService>().Object);
        _ = Services.AddLogging();
        SetRendererInfo(new RendererInfo("Server", true));
    }

    [Fact]
    public void QueryApiConfigPage_Breadcrumb_ShowsSourcesQueryApiThenDeviceConfigAsCurrentPage()
    {
        IRenderedComponent<QueryApiConfig> cut = Render<QueryApiConfig>();

        IReadOnlyList<IElement> items = cut.FindAll("nav ol > li");
        Assert.Equal(3, items.Count);
        Assert.Equal("/accounts", items[0].QuerySelector("a")?.GetAttribute("href"));
        Assert.Equal("/query", items[1].QuerySelector("a")?.GetAttribute("href"));
        Assert.Equal("page", items[2].GetAttribute("aria-current"));
        Assert.Equal("Device Config", items[2].TextContent.Trim());
    }

    [Fact]
    public void QueryApiConfigPage_SharedDeviceIdSet_ShowsInDeviceTextBox()
    {
        Services.GetRequiredService<IQueryApiFilterState>().SetDeviceId("dev-7");

        IRenderedComponent<QueryApiConfig> cut = Render<QueryApiConfig>();

        IRenderedComponent<SfTextBox> textBox = cut.FindComponents<SfTextBox>().Single(c => c.Instance.ID == "query-api-config-device-filter");
        Assert.Equal("dev-7", textBox.Instance.Value);
    }

    [Fact]
    public async Task QueryApiConfigPage_DeviceTextBoxChanged_CallsSetDeviceIdOnSharedState()
    {
        IRenderedComponent<QueryApiConfig> cut = Render<QueryApiConfig>();
        IRenderedComponent<SfTextBox> textBox = cut.FindComponents<SfTextBox>().Single(c => c.Instance.ID == "query-api-config-device-filter");

        await cut.InvokeAsync(() => textBox.Instance.ValueChanged.InvokeAsync("dev-9"));

        Assert.Equal("dev-9", Services.GetRequiredService<IQueryApiFilterState>().DeviceId);
    }

    [Fact]
    public void QueryApiConfigPage_DeviceIdSetBeforeNavigation_IsReflectedOnConfigPage()
    {
        // Simulates a DeviceId chosen on another Query API subpage: same circuit, so same shared state.
        IQueryApiFilterState filterState = Services.GetRequiredService<IQueryApiFilterState>();
        filterState.SetDeviceId("dev-42");

        IRenderedComponent<QueryApiConfig> cut = Render<QueryApiConfig>();

        IRenderedComponent<SfTextBox> textBox = cut.FindComponents<SfTextBox>().Single(c => c.Instance.ID == "query-api-config-device-filter");
        Assert.Equal("dev-42", textBox.Instance.Value);
    }
}
