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

public class QueryApiConfigPage_Tests : BunitContext
{
    public QueryApiConfigPage_Tests()
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
        Services.AddSingleton(new Mock<IDeviceDiscoveryService>().Object);
        Services.AddSingleton(new Mock<IEventAndConfigService>().Object);
        Services.AddLogging();
        SetRendererInfo(new RendererInfo("Server", true));
    }

    [Fact]
    public void QueryApiConfigPage_Breadcrumb_ShowsSourcesQueryApiThenDeviceConfigAsCurrentPage()
    {
        var cut = Render<QueryApiConfig>();

        var items = cut.FindAll("nav ol > li");
        Assert.Equal(3, items.Count);
        Assert.Equal("/accounts", items[0].QuerySelector("a")?.GetAttribute("href"));
        Assert.Equal("/query", items[1].QuerySelector("a")?.GetAttribute("href"));
        Assert.Equal("page", items[2].GetAttribute("aria-current"));
        Assert.Equal("Device Config", items[2].TextContent.Trim());
    }
}
