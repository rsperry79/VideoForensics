namespace VideoForensics.Ui.Shared.Services.Evidence;

/// <summary>One WiFi RSSI reading at a point in time, for plotting.</summary>
public sealed record RssiSample(DateTime TimestampUtc, int Rssi);

/// <summary>
/// Pure geometry helper for the per-device RSSI-over-time plot in <c>SnapshotStrip</c>: maps RSSI
/// samples and timestamps to SVG coordinates without touching any rendering API, so the mapping can
/// be unit tested directly rather than only through a rendered component.
/// </summary>
public static class RssiPlot
{
    /// <summary>
    /// Builds the space-separated "x,y x,y ..." point list for an SVG &lt;polyline&gt;, plotting
    /// <paramref name="samples"/> left-to-right over [<paramref name="rangeStartUtc"/>, <paramref
    /// name="rangeEndUtc"/>) and top-to-bottom over [<paramref name="maxRssi"/> (strongest, top),
    /// <paramref name="minRssi"/> (weakest, bottom)]. A sample whose timestamp falls outside the
    /// range is clamped to the nearest edge rather than dropped, so a reading just outside the
    /// visible window still anchors the line instead of leaving a dangling gap. RSSI values are
    /// likewise clamped into [<paramref name="minRssi"/>, <paramref name="maxRssi"/>].
    /// </summary>
    public static string BuildPolylinePoints(
        IReadOnlyList<RssiSample> samples,
        DateTime rangeStartUtc,
        DateTime rangeEndUtc,
        double width,
        double height,
        int minRssi = -100,
        int maxRssi = -30)
    {
        if (samples.Count == 0 || rangeEndUtc <= rangeStartUtc)
        {
            return string.Empty;
        }

        var totalTicks = (double)(rangeEndUtc - rangeStartUtc).Ticks;
        var rssiSpan = (double)(maxRssi - minRssi);

        var points = samples
            .OrderBy(s => s.TimestampUtc)
            .Select(s =>
            {
                var x = Math.Clamp((s.TimestampUtc - rangeStartUtc).Ticks / totalTicks * width, 0, width);
                var clampedRssi = Math.Clamp(s.Rssi, minRssi, maxRssi);
                var normalizedStrength = rssiSpan == 0 ? 0 : (clampedRssi - minRssi) / rssiSpan;
                var y = height - (normalizedStrength * height); // stronger signal plots nearer the top
                return $"{x.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)},{y.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}";
            });

        return string.Join(' ', points);
    }

    /// <summary>
    /// The horizontal position (in the same width used by <see cref="BuildPolylinePoints"/>) of a
    /// given timestamp within the range - used to draw the scrub-position marker line. A timestamp
    /// outside the range is clamped to the nearest edge; a zero-width range always returns 0.
    /// </summary>
    public static double MarkerX(DateTime timestampUtc, DateTime rangeStartUtc, DateTime rangeEndUtc, double width)
    {
        var totalTicks = (rangeEndUtc - rangeStartUtc).Ticks;
        if (totalTicks <= 0)
        {
            return 0;
        }

        return Math.Clamp((timestampUtc - rangeStartUtc).Ticks / (double)totalTicks * width, 0, width);
    }
}
