using System.Globalization;

namespace VideoForensics.Client.Ui.Shared.Services.Signal;

/// <summary>
/// Geometry for the signal chart. The SVG uses a fixed view box so the axes and bars keep their
/// proportions at any width; these helpers map readings and pointer positions into that box and back.
/// </summary>
public static class SignalChartModel
{
    /// <summary>View box width in SVG units; the left margin holds the dBm labels.</summary>
    public const double ViewWidth = 640;

    /// <summary>View box height in SVG units; the bottom margin holds the time labels.</summary>
    public const double ViewHeight = 240;

    /// <summary>Left edge of the plot area, leaving room for the dBm labels.</summary>
    public const double PlotLeft = 48;

    /// <summary>Top edge of the plot area.</summary>
    public const double PlotTop = 8;

    /// <summary>Width of the plot area in SVG units.</summary>
    public const double PlotWidth = 584;

    /// <summary>Height of the plot area in SVG units.</summary>
    public const double PlotHeight = 192;

    /// <summary>Strongest signal on the axis. Ring reports RSSI, so values closer to zero are stronger.</summary>
    public const int StrongestDbm = -30;

    /// <summary>Weakest signal on the axis; anything below this is clamped to the bottom edge.</summary>
    public const int WeakestDbm = -100;

    /// <summary>The dBm values labelled on the y-axis, strongest first.</summary>
    public static readonly int[] DbmTicks = [-30, -50, -70, -90];

    /// <summary>Maps a time onto the x-axis: the window start is the left edge, the window end the right edge.</summary>
    public static double MapX(DateTime utc, DateTime from, DateTime to)
    {
        double span = (to - from).TotalSeconds;
        if (span <= 0)
        {
            return PlotLeft;
        }

        return PlotLeft + (utc - from).TotalSeconds / span * PlotWidth;
    }

    /// <summary>Maps a dBm value onto the y-axis. Stronger signal sits higher; values beyond the axis are clamped.</summary>
    public static double MapDbm(double dbm)
    {
        double clamped = Math.Clamp(dbm, WeakestDbm, StrongestDbm);
        return PlotTop + (StrongestDbm - clamped) / (StrongestDbm - WeakestDbm) * PlotHeight;
    }

    /// <summary>
    /// The polyline points for the readings inside the window, in time order. Readings with no signal,
    /// or outside the window, are left out so they never draw a line to a made-up position.
    /// </summary>
    public static string Polyline(IEnumerable<SignalSample> samples, DateTime from, DateTime to)
    {
        ArgumentNullException.ThrowIfNull(samples);

        return string.Join(' ', samples
            .Where(s => s.RssiDbm is not null && s.CapturedAtUtc >= from && s.CapturedAtUtc <= to)
            .OrderBy(s => s.CapturedAtUtc)
            .Select(s => $"{MapX(s.CapturedAtUtc, from, to):0.#},{MapDbm(s.RssiDbm!.Value):0.#}"));
    }

    /// <summary>
    /// The stretches to overlay on the chart: no-signal checks (gray), low-signal checks and gaps of two or more
    /// missed checks (light red). Each reading covers the time until the next check, capped at one expected
    /// interval, so a gap is only flagged as missed checks once it is long enough to hold two of them.
    /// Adjacent stretches of the same kind are merged. Gaps before the first reading in the window are not flagged.
    /// </summary>
    public static IReadOnlyList<SignalPeriod> Periods(IEnumerable<SignalSample> samples, DateTime from, DateTime to, TimeSpan expectedInterval)
    {
        ArgumentNullException.ThrowIfNull(samples);
        if (expectedInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedInterval), "The expected check interval must be positive.");
        }

        List<SignalSample> ordered = samples
            .Where(s => s.CapturedAtUtc >= from && s.CapturedAtUtc <= to)
            .OrderBy(s => s.CapturedAtUtc)
            .ToList();

        var periods = new List<SignalPeriod>();
        for (int i = 0; i < ordered.Count; i++)
        {
            SignalSample sample = ordered[i];
            DateTime next = i + 1 < ordered.Count ? ordered[i + 1].CapturedAtUtc : to;
            DateTime coverageEnd = next < sample.CapturedAtUtc + expectedInterval ? next : sample.CapturedAtUtc + expectedInterval;

            if (sample.RssiDbm is null)
            {
                periods.Add(new SignalPeriod(SignalPeriodKind.NoSignal, sample.CapturedAtUtc, coverageEnd));
            }
            else if (sample.RssiDbm.Value < SignalStats.WeakThresholdDbm)
            {
                periods.Add(new SignalPeriod(SignalPeriodKind.Weak, sample.CapturedAtUtc, coverageEnd));
            }

            // Whole expected intervals in the gap, minus the check that should have covered the first one.
            long missed = (next - sample.CapturedAtUtc).Ticks / expectedInterval.Ticks - 1;
            if (missed >= MissedChecksToFlag)
            {
                periods.Add(new SignalPeriod(SignalPeriodKind.MissedChecks, sample.CapturedAtUtc + expectedInterval, next));
            }
        }

        return periods
            .GroupBy(p => p.Kind)
            .SelectMany(group => MergeAdjacent(group.OrderBy(p => p.StartUtc)))
            .OrderBy(p => p.StartUtc)
            .ToList();
    }

    /// <summary>Two or more missed checks in a row are flagged. One missed check is normal jitter on the poll.</summary>
    public const int MissedChecksToFlag = 2;

    /// <summary>
    /// How often Ring health is checked. Matches the base poll in DeviceHealthSyncService; cameras on battery
    /// are polled less often, so their gaps can be flagged even though they are expected.
    /// </summary>
    public static readonly TimeSpan ExpectedCheckInterval = TimeSpan.FromMinutes(15);

    private static IEnumerable<SignalPeriod> MergeAdjacent(IEnumerable<SignalPeriod> ordered)
    {
        SignalPeriod? current = null;
        foreach (SignalPeriod period in ordered)
        {
            if (current is not null && period.StartUtc <= current.EndUtc)
            {
                current = current with { EndUtc = period.EndUtc > current.EndUtc ? period.EndUtc : current.EndUtc };
                continue;
            }

            if (current is not null)
            {
                yield return current;
            }

            current = period;
        }

        if (current is not null)
        {
            yield return current;
        }
    }

    /// <summary>Evenly spaced times across the window, including both edges. Used for the x-axis labels.</summary>
    public static IReadOnlyList<DateTime> TimeTicks(DateTime from, DateTime to, int count)
    {
        if (count < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "At least two ticks are needed to show both window edges.");
        }

        TimeSpan step = (to - from) / (count - 1);
        return Enumerable.Range(0, count).Select(i => from + step * i).ToList();
    }

    /// <summary>
    /// Converts a selection drag, given as fractions of the plot width (0 = window start, 1 = window end),
    /// into a time range. The drag can run either way. Returns null for a zero-width selection.
    /// </summary>
    public static (DateTime start, DateTime end)? SelectionRange(double startFraction, double endFraction, DateTime from, DateTime to)
    {
        double low = Math.Min(startFraction, endFraction);
        double high = Math.Max(startFraction, endFraction);
        if (high - low <= 0)
        {
            return null;
        }

        double span = (to - from).TotalSeconds;
        return (from.AddSeconds(low * span), from.AddSeconds(high * span));
    }

    /// <summary>
    /// Turns a pointer offset in CSS pixels into a 0..1 fraction of the plot width. The browser reports pixels,
    /// but the chart scales with the page, so the element's rendered width is needed to get a fraction.
    /// </summary>
    public static double FractionFromPixel(double offsetPixels, double elementWidthPixels)
    {
        if (elementWidthPixels <= 0)
        {
            return 0;
        }

        return Math.Clamp(offsetPixels / elementWidthPixels, 0, 1);
    }

    /// <summary>
    /// Like <see cref="FractionFromPixel"/>, but for a pointer offset on the whole chart (axis labels included).
    /// The offset is shifted past the left margin and scaled to the plot, so a fraction of 0 is the window start.
    /// </summary>
    public static double FractionFromCanvasOffset(double offsetPixels, double canvasWidthPixels)
    {
        double scale = canvasWidthPixels / ViewWidth;
        return FractionFromPixel(offsetPixels - PlotLeft * scale, PlotWidth * scale);
    }

    /// <summary>Time label for an axis tick. Short windows show the clock time; longer ones show the date.</summary>
    public static string FormatTick(DateTime utc, TimeSpan windowSpan)
    {
        return windowSpan.TotalHours <= 48
            ? utc.ToString("HH:mm", CultureInfo.InvariantCulture)
            : utc.ToString("MMM d", CultureInfo.InvariantCulture);
    }
}
