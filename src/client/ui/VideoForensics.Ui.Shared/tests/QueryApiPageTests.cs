namespace VideoForensics.Ui.Shared.Tests;

using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Moq;
using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Ui.Shared.Pages;
using VideoForensics.Ui.Shared.Resources;
using VideoForensics.Ui.Shared.Services.Scope;
using VideoForensics.Ui.Shared.Services.QueryApi;
using Xunit;

public class QueryApiPage_Tests : BunitContext
{
    public QueryApiPage_Tests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        Services.AddLocalization();
        Services.AddSingleton<IStringLocalizer<SharedResources>>(TestLocalizer.Create());
        Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();
        Services.AddSingleton(new ScopeState());
        Services.AddScoped<VideoForensics.Ui.Shared.Services.RightPanelContentService>();
        Services.AddSingleton(new Mock<IProviderAuthService>().Object);
        Services.AddSingleton(new Mock<IDeviceDiscoveryService>().Object);
        Services.AddSingleton(new Mock<IEventAndConfigService>().Object);
        Services.AddLogging();
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
}
