using System.Text.RegularExpressions;

using AngleSharp.Dom;

using Bunit;

using VideoForensics.Client.Ui.Shared.Components.Review;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Review;

/// <summary>Accessibility fixes from the layout review: named icon buttons, a group role on the filter panel, and valid markup inside the preview buttons.</summary>
public class ReviewAccessibilityU11_Tests : ReviewTestContext
{
    [Fact]
    public void MainLayout_GlyphButtons_HaveAriaLabels()
    {
        // Arrange
        string[] lines = RepoFile.ReadUiSharedSource("Layout/MainLayout.razor").Split('\n');
        string[] glyphs = lines.Where(line => line.Contains("<SfButton") && line.Contains("title=\"@L[")).ToArray();

        // Act and Assert: every icon-only button names itself for a screen reader, not only by tooltip
        Assert.NotEmpty(glyphs);
        Assert.All(glyphs, line => Assert.Contains("aria-label=", line));
    }

    [Fact]
    public void ReviewFilterPanel_Rendered_IsAGroupNamedByItsTitle()
    {
        // Arrange and Act
        IRenderedComponent<ReviewFilterPanel> cut = Render<ReviewFilterPanel>(p => p
            .Add(x => x.State, ReviewState));

        // Assert
        IElement panel = cut.Find("[data-testid='review-filter-panel']");
        Assert.Equal("group", panel.GetAttribute("role"));
        Assert.False(string.IsNullOrWhiteSpace(panel.GetAttribute("aria-label")));
    }

    [Fact]
    public void PreviewPlaceholders_AreSpans_NotDivsInsideButtons()
    {
        // Assert: a div inside a button is invalid HTML and the browser splits it; the placeholders are inline text
        string single = RepoFile.ReadUiSharedSource("Components/Viewer/SingleCameraView.razor");
        string grid = RepoFile.ReadUiSharedSource("Components/Viewer/CameraGrid.razor");

        Assert.Contains("<span class=\"single-camera-placeholder\"", single);
        Assert.Contains("<span class=\"camera-tile-placeholder\"", grid);
        Assert.DoesNotMatch(new Regex("<div class=\"(single-camera|camera-tile)-placeholder\""), single + grid);
    }
}
