using VideoForensics.Client.Ui.Shared.Services.Review;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Review;

public class ReviewWindowSizingTests
{
    [Fact]
    public void ReviewWindowSizing_UnmeasuredWindow_ReturnsNullSoTheStylesheetDefaultsApply()
    {
        Assert.Null(ReviewWindowSizing.StyleFor(0));
    }

    [Fact]
    public void ReviewWindowSizing_MeasuredWindow_SizesMediaAndTimelineFromTheWindowHeight()
    {
        string? style = ReviewWindowSizing.StyleFor(1000);

        Assert.Equal("--review-media-height:740px;--review-timeline-height:872px", style);
    }

    [Fact]
    public void ReviewWindowSizing_ShortWindow_ClampsToTheMinimumHeight()
    {
        string? style = ReviewWindowSizing.StyleFor(300);

        Assert.Equal($"--review-media-height:{ReviewWindowSizing.MinHeightPx:0}px;--review-timeline-height:{ReviewWindowSizing.MinHeightPx:0}px", style);
    }
}
