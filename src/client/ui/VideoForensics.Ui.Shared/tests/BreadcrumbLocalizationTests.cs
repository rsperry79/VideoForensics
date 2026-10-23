
using AngleSharp.Dom;

using Bunit;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;

using Moq;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Ui.Shared.Pages;
using VideoForensics.Client.Ui.Shared.Resources;
using VideoForensics.Client.Ui.Shared.Services;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;
/// <summary>
/// Page-level breadcrumb tests: every crumb label must come from <see cref="IStringLocalizer{T}"/>
/// (tagged as "L:Key" by <see cref="TaggingLocalizer"/>), never a hard-coded English literal.
/// </summary>
public class BreadcrumbLocalizationTests : BunitContext
{
    public BreadcrumbLocalizationTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        _ = Services.AddLocalization();
        _ = Services.AddSingleton<IStringLocalizer<SharedResources>>(new TaggingLocalizer());
        _ = Services.AddSingleton<Syncfusion.Blazor.ISyncfusionStringLocalizer, Syncfusion.Blazor.SyncfusionStringLocalizer>();
        _ = Services.AddSingleton<Syncfusion.Blazor.GlobalOptions>();
        _ = Services.AddScoped<Syncfusion.Blazor.SyncfusionBlazorService>();
        _ = Services.AddScoped(_ => new PairedSessionState(JSInterop.JSRuntime));
        _ = Services.AddScoped(_ => new Mock<WebAuthnClient>(Mock.Of<IJSRuntime>(), Mock.Of<ISelfApiHttpClientFactory>()).Object);
        _ = Services.AddScoped<StepUpService>();
        _ = Services.AddScoped(_ => new Mock<IAdminOperatorService>().Object);
        _ = Services.AddScoped(_ => new Mock<IEvidenceExportService>().Object);
        _ = Services.AddScoped(_ => Mock.Of<Data.Common.Contracts.ICaseRepository>());
        _ = Services.AddScoped(_ => new Mock<ISelfApiHttpClientFactory>().Object);
        _ = Services.AddLogging();
    }

    [Fact]
    public void ExportEvidence_Breadcrumb_LabelsComeFromLocalizer()
    {
        IRenderedComponent<ExportEvidence> cut = Render<ExportEvidence>();

        IReadOnlyList<IElement> items = cut.FindAll("nav ol > li");
        Assert.Equal(2, items.Count);
        Assert.Equal("L:Events", items[0].QuerySelector("a")?.TextContent.Trim());
        Assert.Equal("L:Export Evidence", items[1].TextContent.Trim());
    }

    [Fact]
    public void OperatorDetails_Breadcrumb_LabelsComeFromLocalizer()
    {
        IRenderedComponent<OperatorDetails> cut = Render<OperatorDetails>(p => p.Add(c => c.Id, Guid.NewGuid()));

        IReadOnlyList<IElement> items = cut.FindAll("nav ol > li");
        Assert.Equal(2, items.Count);
        Assert.Equal("L:Operators", items[0].QuerySelector("a")?.TextContent.Trim());
        Assert.Equal("Operator Details", items[1].TextContent.Trim());
    }

    [Fact]
    public void SecurityOperators_Breadcrumb_LabelsComeFromLocalizer()
    {
        IRenderedComponent<SecurityOperators> cut = Render<SecurityOperators>();

        IReadOnlyList<IElement> items = cut.FindAll("nav ol > li");
        Assert.Equal(2, items.Count);
        Assert.Equal("L:Settings", items[0].QuerySelector("a")?.TextContent.Trim());
        Assert.Equal("L:Operators", items[1].TextContent.Trim());
    }
}
