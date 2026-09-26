namespace VideoForensics.Ui.Shared.Services.Evidence;

using System.Globalization;

/// <summary>
/// Immutable viewport state for image pan and zoom operations.
/// </summary>
public sealed class ImageViewport
{
    /// <summary>The zoom scale (1 = 100%, 2 = 200%, etc.). Clamped between 1 and 8.</summary>
    public double Scale { get; init; } = 1;

    /// <summary>Horizontal offset in pixels due to panning.</summary>
    public double OffsetX { get; init; } = 0;

    /// <summary>Vertical offset in pixels due to panning.</summary>
    public double OffsetY { get; init; } = 0;

    /// <summary>
    /// Zoom in by multiplying scale by 1.25, clamped to max 8.
    /// </summary>
    public ImageViewport ZoomIn()
    {
        var newScale = Math.Min(Scale * 1.25, 8);
        return new ImageViewport { Scale = newScale, OffsetX = OffsetX, OffsetY = OffsetY };
    }

    /// <summary>
    /// Zoom out by dividing scale by 1.25, clamped to min 1.
    /// </summary>
    public ImageViewport ZoomOut()
    {
        var newScale = Math.Max(Scale / 1.25, 1);
        return new ImageViewport { Scale = newScale, OffsetX = OffsetX, OffsetY = OffsetY };
    }

    /// <summary>
    /// Reset zoom to 1 and offset to (0, 0).
    /// </summary>
    public ImageViewport Fit()
    {
        return new ImageViewport { Scale = 1, OffsetX = 0, OffsetY = 0 };
    }

    /// <summary>
    /// Pan by the given delta. Only effective when scale > 1; ignored at scale 1.
    /// </summary>
    public ImageViewport PanBy(double deltaX, double deltaY)
    {
        if (Scale <= 1)
        {
            // Pan has no effect at scale 1
            return this;
        }

        return new ImageViewport
        {
            Scale = Scale,
            OffsetX = OffsetX + deltaX,
            OffsetY = OffsetY + deltaY
        };
    }

    /// <summary>
    /// Generate a CSS transform string for this viewport state.
    /// Returns "translate(Xpx, Ypx) scale(S)" using invariant culture.
    /// </summary>
    public string ToCssTransform()
    {
        var offsetXStr = OffsetX.ToString("0", CultureInfo.InvariantCulture);
        var offsetYStr = OffsetY.ToString("0", CultureInfo.InvariantCulture);
        var scaleStr = Scale.ToString("G", CultureInfo.InvariantCulture);
        return $"translate({offsetXStr}px, {offsetYStr}px) scale({scaleStr})";
    }
}
