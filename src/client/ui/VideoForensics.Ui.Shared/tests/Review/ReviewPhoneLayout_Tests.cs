using Bunit;

using Microsoft.Extensions.DependencyInjection;

using Moq;

using VideoForensics.Client.Common.Viewer;
using VideoForensics.Client.Ui.Shared.Components.Review;
using VideoForensics.Client.Ui.Shared.Services;
using VideoForensics.Client.Ui.Shared.Services.Review;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Review;

/// <summary>The review layout under 600px: the side column goes under the stage, the timeline lies flat, and popups stay in the sheet.</summary>
public class ReviewPhoneLayout_Tests : ReviewTestContext
{
    private static string ReadShellCss() => RepoFile.ReadUiSharedSource("Components/Review/ReviewShell.razor.css");

    [Fact]
    public void ReviewShellStylesheet_StacksUnderSixHundredPixels()
    {
        // Arrange
        string css = ReadShellCss();

        // Assert: under 600px the rows are header, stage, side, bottom: one column, nothing scrolls sideways
        Assert.Contains("@media (max-width: 599px)", css);
        Assert.Matches(@"@media \(max-width: 599px\)\s*\{[^}]*grid-template-columns:\s*minmax\(0, 1fr\);", css);
        Assert.Contains("\"side\"", css);
    }

    [Fact]
    public void ReviewTimelinePanel_MobileViewport_UsesHorizontalStrip()
    {
        // Arrange
        Viewport.SetupGet(v => v.IsMobile).Returns(true);

        // Act
        IRenderedComponent<ReviewTimelinePanel> cut = Render<ReviewTimelinePanel>();

        // Assert: a vertical strip under the stage would be too tall for a phone
        string classes = cut.Find("[data-testid='timeline-strip']").ClassName ?? string.Empty;
        Assert.DoesNotContain("review-timeline-vertical", classes);
    }

    [Fact]
    public void ReviewTimelinePanel_DesktopViewport_UsesVerticalStrip()
    {
        // Arrange
        Viewport.SetupGet(v => v.IsMobile).Returns(false);

        // Act
        IRenderedComponent<ReviewTimelinePanel> cut = Render<ReviewTimelinePanel>();

        // Assert
        Assert.Contains("review-timeline-vertical", cut.Find("[data-testid='timeline-strip']").ClassName ?? string.Empty);
    }
}
