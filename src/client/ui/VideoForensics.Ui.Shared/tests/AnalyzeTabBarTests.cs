namespace VideoForensics.Ui.Shared.Tests;

using Bunit;
using Xunit;

/// <summary>The analysis switcher is a Bootstrap nav-tabs bar, not a row of buttons.</summary>
public class Analyze_TabBar_Tests : AnalyzePageTestBase
{
    [Fact]
    public void Analyze_TabsRenderAsNavTabBar_WithActiveTabMarked()
    {
        var component = RenderPage();

        var bar = component.Find("[data-testid='analysis-tabs']");
        Assert.Contains("nav-tabs", bar.ClassName);

        var reports = component.Find("[data-testid='analysis-reports']");
        Assert.Contains("nav-link", reports.ClassName);
        Assert.Contains("active", reports.ClassName);
        Assert.Equal("tab", reports.GetAttribute("role"));
        Assert.Equal("true", reports.GetAttribute("aria-selected"));

        var anomalies = component.Find("[data-testid='analysis-anomalies']");
        Assert.Contains("nav-link", anomalies.ClassName);
        Assert.DoesNotContain("active", anomalies.ClassName);
        Assert.Equal("false", anomalies.GetAttribute("aria-selected"));
    }
}
