using System.Globalization;

namespace VideoForensics.Client.Ui.Shared.Services.Review;

/// <summary>
/// Turns the measured window height into the sizes the review page uses for its media and timeline. Pure functions,
/// so the sizing is tested without a browser.
/// </summary>
public static class ReviewWindowSizing
{
    /// <summary>Space above and below the media on the review page: header, tabs, transport bar and captions. An estimate.</summary>
    public const double MediaChromePx = 260;

    /// <summary>Space the timeline aside leaves for the page header above it.</summary>
    public const double TimelineChromePx = 128;

    /// <summary>The smallest height either element is given, so a very short window still shows a usable player.</summary>
    public const double MinHeightPx = 240;

    /// <summary>
    /// CSS custom properties for the review page's media and timeline heights, or null when the window has not been
    /// measured yet. Null keeps the stylesheet's viewport-based defaults.
    /// </summary>
    public static string? StyleFor(double windowHeightPx)
    {
        if (windowHeightPx <= 0)
        {
            return null;
        }

        double media = Math.Max(MinHeightPx, windowHeightPx - MediaChromePx);
        double timeline = Math.Max(MinHeightPx, windowHeightPx - TimelineChromePx);
        return string.Create(CultureInfo.InvariantCulture, $"--review-media-height:{media:0}px;--review-timeline-height:{timeline:0}px");
    }
}
