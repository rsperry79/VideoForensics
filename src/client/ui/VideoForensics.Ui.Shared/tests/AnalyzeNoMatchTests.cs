
using Bunit;

using Moq;

using VideoForensics.Ui.Shared.Pages;

using Xunit;

namespace VideoForensics.Ui.Shared.Tests;
/// <summary>When the filters match no device, Analyze shows a notice and runs no report.</summary>
public class Analyze_NoMatch_Tests : AnalyzePageTestBase
{
    [Fact]
    public void Analyze_NoMatchFilter_ShowsNoticeAndRunsNoReport()
    {
        ScopeState.Set(ScopeState.Current with { MatchesNoDevices = true });

        IRenderedComponent<Analyze> component = RenderPage();

        Assert.NotEmpty(component.FindAll("[data-testid='no-devices-match']"));
        ReportGenerationServiceMock.Verify(
            m => m.BuildForensicAnalysisReportAsync(It.IsAny<Guid?>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
