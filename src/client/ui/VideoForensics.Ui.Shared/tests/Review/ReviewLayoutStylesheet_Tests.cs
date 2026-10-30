using System.Text.RegularExpressions;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

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
    public void CameraGridStylesheet_Grid_IsDisplayGridAndSingleColumnOnNarrowScreens()
    {
        // Act
        string css = RepoFile.ReadUiSharedSource("Components/Viewer/CameraGrid.razor.css");

        // Assert
        Assert.Contains("display: grid", css);
        Assert.Contains("@media (max-width: 900px)", css);
        Assert.Contains("grid-template-columns: 1fr !important", css);
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
        string css = RepoFile.ReadUiSharedSource("Components/Review/ReviewTopBar.razor.css");

        // Act
        int backdrop = ZIndexOf(css, @"\.review-popup-backdrop\s*\{([^}]*)\}");
        int popup = ZIndexOf(css, @"\.review-popup\s*\{([^}]*)\}");

        // Assert
        Assert.True(backdrop < popup, $"The backdrop z-index ({backdrop}) must be below the popup z-index ({popup}).");
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
    public void ReviewPageStylesheet_Body_IsAFlexRowWithAShrinkableContentColumn()
    {
        // Arrange
        string css = RepoFile.ReadUiSharedSource("Pages/ReviewPage.razor.css");
        Match body = Regex.Match(css, @"\.review-body\s*\{([^}]*)\}");
        Match content = Regex.Match(css, @"\.review-content\s*\{([^}]*)\}");

        // Assert
        Assert.True(body.Success, "The review body rule must exist.");
        Assert.Contains("display: flex", body.Groups[1].Value);
        Assert.True(content.Success, "The review content rule must exist.");
        Assert.Matches(new Regex(@"flex:\s*1"), content.Groups[1].Value);
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
