using System.Text.RegularExpressions;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Review;

/// <summary>
/// The review and viewer layout rules. The components have no browser test, so these read the scoped stylesheets and assert
/// the declarations that keep the video, the camera grid, the preview strip and the timeline within their container.
/// </summary>
public class ReviewLayoutStylesheet_Tests
{
    [Fact]
    public void ReviewPlayerStylesheet_Video_FitsItsColumnAndKeepsItsAspect()
    {
        // Act
        string css = RepoFile.ReadUiSharedSource("Components/Viewer/ReviewPlayer.razor.css");

        // Assert
        Assert.True(css.Contains("max-width") || css.Contains("width: 100%"), "The video must not be wider than its column.");
        Assert.Contains("max-height", css);
        Assert.Contains("object-fit: contain", css);
    }

    [Fact]
    public void ReviewStylesheets_NoObjectFitCover_InReviewComponents()
    {
        // Arrange: the review pages use contain everywhere so evidence frames are never cropped
        string media = RepoFile.ReadUiSharedSource("Components/Review/MediaFit.razor.css");
        string player = RepoFile.ReadUiSharedSource("Components/Viewer/ReviewPlayer.razor.css");

        // Assert
        Assert.DoesNotContain("object-fit: cover", media);
        Assert.DoesNotContain("object-fit: cover", player);
    }

    [Fact]
    public void ReviewPlayerStylesheet_Stage_HasFixedAspectAndViewportCap()
    {
        // Arrange
        string css = RepoFile.ReadUiSharedSource("Components/Viewer/ReviewPlayer.razor.css");
        Match stage = Regex.Match(css, @"\.review-stage\s*\{([^}]*)\}");

        // Assert
        Assert.True(stage.Success, "The stage rule must exist.");
        Assert.Contains("aspect-ratio: 16 / 9", stage.Groups[1].Value);
        Assert.Contains("max-height: 62vh", stage.Groups[1].Value);
    }

    [Fact]
    public void CameraGridStylesheet_Grid_IsDisplayGridAndSingleColumnOnNarrowScreens()
    {
        // Act
        string css = RepoFile.ReadUiSharedSource("Components/Viewer/CameraGrid.razor.css");

        // Assert
        Assert.Contains("display: grid", css);
        Assert.Contains("repeat(auto-fit, minmax(280px, 1fr))", css);
        Assert.DoesNotContain("!important", css);
    }

    [Fact]
    public void SingleCameraViewStylesheet_PreviewStrip_ScrollsHorizontally()
    {
        // Act
        string css = RepoFile.ReadUiSharedSource("Components/Viewer/SingleCameraView.razor.css");

        // Assert
        Assert.Contains("overflow-x: auto", css);
        Assert.Contains("display: flex", css);
    }

    [Fact]
    public void ReviewTimelineTabStylesheet_GrabStillBar_HasABottomMargin()
    {
        // Act
        string css = RepoFile.ReadUiSharedSource("Components/Review/ReviewTimelineTab.razor.css");

        // Assert
        Assert.Matches(new Regex(@"\.review-grab-still-bar\s*\{[^}]*margin-bottom:\s*[^;]+;"), css);
    }

    [Fact]
    public void TimelineStripStylesheet_VerticalCursor_StartsRightOfTheLabelGutter()
    {
        // Arrange
        string css = RepoFile.ReadUiSharedSource("Components/Viewer/TimelineStrip.razor.css");
        Match block = Regex.Match(css, @"\.review-timeline-vertical \.review-timeline-cursor-line\s*\{([^}]*)\}");

        // Act
        Match left = Regex.Match(block.Groups[1].Value, @"(?<![-\w])left:\s*([^;]+);");

        // Assert
        Assert.True(block.Success, "The vertical cursor rule must exist.");
        Assert.True(left.Success, "The vertical cursor rule must set left.");
        Assert.NotEqual("0", left.Groups[1].Value.Trim());
        Assert.NotEqual("0px", left.Groups[1].Value.Trim());
    }

    [Fact]
    public void ReviewTopBarStylesheet_PopupBackdrop_SitsBelowThePopups()
    {
        // Arrange
        string css = RepoFile.ReadUiSharedSource("Components/Review/ReviewPopupHost.razor.css");

        // Act
        int backdrop = ZIndexOf(css, @"\.review-popup-backdrop\s*\{([^}]*)\}");
        int popup = ZIndexOf(css, @"\.review-popup\s*\{([^}]*)\}");

        // Assert
        Assert.True(backdrop < popup, $"The backdrop z-index ({backdrop}) must be below the popup z-index ({popup}).");
    }

    [Fact]
    public void ReviewEventFilterStylesheet_PopupAndOpenControl_SitAboveThePageBackdrop()
    {
        // Arrange: the popup styles are scoped per component, so the Event Filter needs its own copy of the camera selector's
        string css = RepoFile.ReadUiSharedSource("Components/Review/ReviewEventFilter.razor.css");
        int backdrop = ZIndexOf(RepoFile.ReadUiSharedSource("Components/Review/ReviewPopupHost.razor.css"), @"\.review-popup-backdrop\s*\{([^}]*)\}");

        // Act
        int popup = ZIndexOf(css, @"\.review-event-selector \.review-popup\s*\{([^}]*)\}");
        int open = ZIndexOf(css, @"\.review-event-selector-open\s*\{([^}]*)\}");

        // Assert: otherwise the backdrop swallows every click in the popup and the rest of the page
        Assert.True(popup > backdrop, $"The popup z-index ({popup}) must be above the backdrop ({backdrop}).");
        Assert.True(open > backdrop, $"The open control z-index ({open}) must be above the backdrop ({backdrop}).");
        Assert.Matches(@"\.review-event-selector \.review-popup\s*\{[^}]*position:\s*absolute", css);
    }

    [Fact]
    public void ReviewPageStylesheet_TimelineAside_IsStickyFixedWidthAndDoesNotShrink()
    {
        // Arrange
        string css = RepoFile.ReadUiSharedSource("Pages/ReviewPage.razor.css");
        Match block = Regex.Match(css, @"\.review-timeline-aside\s*\{([^}]*)\}");

        // Assert
        Assert.True(block.Success, "The timeline aside rule must exist.");
        string rules = block.Groups[1].Value;
        Assert.Contains("position: sticky", rules);
        Assert.Contains("top: 0", rules);
        Assert.Contains("align-self: flex-start", rules);
        Assert.Contains("flex-shrink: 0", rules);
        Assert.Matches(new Regex(@"width:\s*7rem"), rules);
    }

    [Fact]
    public void ReviewShellStylesheet_Stage_IsAShrinkableGridColumn()
    {
        // Arrange: the shell grid replaces the old review body flex row
        string shell = RepoFile.ReadUiSharedSource("Components/Review/ReviewShell.razor.css");
        Match stage = Regex.Match(shell, @"\.review-shell-stage\s*\{([^}]*)\}");
        Match content = Regex.Match(RepoFile.ReadUiSharedSource("Pages/ReviewPage.razor.css"), @"\.review-content\s*\{([^}]*)\}");

        // Assert
        Assert.Contains("minmax(0, 1fr)", shell);
        Assert.True(stage.Success, "The shell stage rule must exist.");
        Assert.Contains("min-width: 0", stage.Groups[1].Value);
        Assert.True(content.Success, "The review content rule must exist.");
        Assert.Contains("min-width: 0", content.Groups[1].Value);
    }

    private static int ZIndexOf(string css, string blockPattern)
    {
        Match block = Regex.Match(css, blockPattern);
        Assert.True(block.Success, $"The stylesheet must contain a rule matching {blockPattern}.");
        Match zIndex = Regex.Match(block.Groups[1].Value, @"z-index:\s*(\d+)");
        Assert.True(zIndex.Success, $"The rule matching {blockPattern} must set z-index.");
        return int.Parse(zIndex.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
    }
}
