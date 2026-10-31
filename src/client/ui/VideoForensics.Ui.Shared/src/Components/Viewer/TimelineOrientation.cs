namespace VideoForensics.Client.Ui.Shared.Components.Viewer;

/// <summary>
/// Direction a <see cref="TimelineStrip"/> runs. Horizontal runs time left to right; vertical runs time top to bottom
/// with the newest time at the top.
/// </summary>
public enum TimelineOrientation
{
    /// <summary>Time runs left to right. The strip's width is <see cref="TimelineStrip.WidthPx"/>.</summary>
    Horizontal,

    /// <summary>Time runs top to bottom, newest at the top. The strip's length is <see cref="TimelineStrip.LengthPx"/>.</summary>
    Vertical,
}
