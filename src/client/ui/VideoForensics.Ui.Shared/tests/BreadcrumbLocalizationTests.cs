namespace VideoForensics.Ui.Shared.Tests;

using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using Moq;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Ui.Shared.Pages;
using VideoForensics.Ui.Shared.Resources;
using VideoForensics.Ui.Shared.Services;
using Xunit;

/// <summary>
/// Page-level breadcrumb tests: every crumb label must come from <see cref="IStringLocalizer{T}"/>
/// (tagged as "L:Key" by <see cref="TaggingLocalizer"/>), never a hard-coded English literal.
/// </summary>
public class BreadcrumbLocalizationTests : BunitContext
{
    public BreadcrumbLocalizationTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddLocalization();
        Services.AddSingleton<IStringLocalizer<SharedResources>>(new TaggingLocalizer());
        Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();
        Services.AddScoped(_ => new PairedSessionState(JSInterop.JSRuntime));
        Services.AddScoped(_ => new Mock<WebAuthnClient>(Mock.Of<IJSRuntime>(), Mock.Of<ISelfApiHttpClientFactory>()).Object);
        Services.AddScoped(_ => new Mock<IAdminOperatorService>().Object);
        Services.AddScoped(_ => new Mock<IEvidenceExportService>().Object);
        Services.AddScoped(_ => new Mock<ISelfApiHttpClientFactory>().Object);
        Services.AddLogging();
    }

    [Fact]
    public void ExportEvidence_Breadcrumb_LabelsComeFromLocalizer()
    {
        var cut = Render<ExportEvidence>();

        var items = cut.FindAll("nav ol > li");
        Assert.Equal(2, items.Count);
        Assert.Equal("L:Events", items[0].QuerySelector("a")?.TextContent.Trim());
        Assert.Equal("L:Export Evidence", items[1].TextContent.Trim());
    }

    [Fact]
    public void OperatorDetails_Breadcrumb_LabelsComeFromLocalizer()
    {
        var cut = Render<OperatorDetails>(p => p.Add(c => c.Id, Guid.NewGuid()));

        var items = cut.FindAll("nav ol > li");
        Assert.Equal(2, items.Count);
        Assert.Equal("L:Operators", items[0].QuerySelector("a")?.TextContent.Trim());
        Assert.Equal("Operator Details", items[1].TextContent.Trim());
    }

    [Fact]
    public void SecurityOperators_Breadcrumb_LabelsComeFromLocalizer()
    {
        var cut = Render<SecurityOperators>();

        var items = cut.FindAll("nav ol > li");
        Assert.Equal(2, items.Count);
        Assert.Equal("L:Settings", items[0].QuerySelector("a")?.TextContent.Trim());
        Assert.Equal("L:Operators", items[1].TextContent.Trim());
    }
}
