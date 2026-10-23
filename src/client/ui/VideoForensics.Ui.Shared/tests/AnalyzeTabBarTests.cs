
using AngleSharp.Dom;

using Bunit;

using VideoForensics.Ui.Shared.Pages;

using Xunit;

namespace VideoForensics.Ui.Shared.Tests;
/// <summary>The analysis switcher is a Bootstrap nav-tabs bar, not a row of buttons.</summary>
public class Analyze_TabBar_Tests : AnalyzePageTestBase
{
    [Fact]
    public void Analyze_TabsRenderAsNavTabBar_WithActiveTabMarked()
    {
        IRenderedComponent<Analyze> component = RenderPage();

        IElement bar = component.Find("[data-testid='analysis-tabs']");
        Assert.Contains("nav-tabs", bar.ClassName);

        IElement reports = component.Find("[data-testid='analysis-reports']");
        Assert.Contains("nav-link", reports.ClassName);
        Assert.Contains("active", reports.ClassName);
        Assert.Equal("tab", reports.GetAttribute("role"));
        Assert.Equal("true", reports.GetAttribute("aria-selected"));

        IElement anomalies = component.Find("[data-testid='analysis-anomalies']");
        Assert.Contains("nav-link", anomalies.ClassName);
        Assert.DoesNotContain("active", anomalies.ClassName);
        Assert.Equal("false", anomalies.GetAttribute("aria-selected"));
    }
}
