namespace VideoForensics.Client.Ui.Shared.Services.Signal;

/// <summary>
/// The kbps range the bit rate chart's y-axis covers. It is fitted to the displayed readings so small changes stay
/// readable. Same shape as <see cref="SignalScale"/>, but sized for kbps: gridline steps scale from 1 kbps to tens of
/// thousands, and the axis never goes below zero because a bit rate cannot be negative.
/// </summary>
/// <param name="LowKbps">The value at the bottom of the plot.</param>
/// <param name="HighKbps">The value at the top of the plot.</param>
public readonly record struct BitrateScale(double LowKbps, double HighKbps)
{
    /// <summary>Fraction of the displayed min-to-max delta added above and below the readings.</summary>
    public const double Padding = 0.15;

    /// <summary>Smallest span the axis will show, so a flat reading still has a readable scale.</summary>
    public const double MinimumSpanKbps = 10;

    /// <summary>Gridlines the axis aims for.</summary>
    private const int TargetTickCount = 5;

    /// <summary>The axis used when there are no readings to fit: 0 to 10000 kbps.</summary>
    public static BitrateScale Default => new(0, 10000);

    /// <summary>
    /// Fits the axis to a displayed minimum and maximum: 15% of the delta is added on each side. A flat reading is
    /// widened to <see cref="MinimumSpanKbps"/>. The bottom is held at zero or above, and the top moves up to keep the span.
    /// </summary>
    public static BitrateScale Fit(double min, double max)
    {
        double delta = max - min;
        double padding = delta * Padding;
        double low = min - padding;
        double high = max + padding;

        double shortfall = MinimumSpanKbps - (high - low);
        if (shortfall > 0)
        {
            low -= shortfall / 2;
            high += shortfall / 2;
        }

        if (low < 0)
        {
            high -= low;
            low = 0;
        }

        return new BitrateScale(low, high);
    }

    /// <summary>The y coordinate for a kbps value. A higher bit rate sits higher; values beyond the scale are clamped.</summary>
    public double MapY(double kbps)
    {
        double clamped = Math.Clamp(kbps, LowKbps, HighKbps);
        return SignalChartModel.PlotTop + ((HighKbps - clamped) / (HighKbps - LowKbps) * SignalChartModel.PlotHeight);
    }

    /// <summary>
    /// Whole-kbps gridline values inside the scale, about five of them. The step is the smallest of 1, 2 or 5 times a
    /// power of ten that keeps the count near the target, so it suits any span without a fixed list to run off the end of.
    /// </summary>
    public IReadOnlyList<int> Ticks()
    {
        double span = HighKbps - LowKbps;
        if (span <= 0)
        {
            return [];
        }

        double rough = span / TargetTickCount;
        double magnitude = Math.Pow(10, Math.Floor(Math.Log10(rough)));
        double step = new[] { 1.0, 2.0, 5.0, 10.0 }.Select(m => m * magnitude).First(s => s >= rough);
        int wholeStep = Math.Max(1, (int)Math.Round(step));

        var ticks = new List<int>();
        for (long value = (long)Math.Ceiling(LowKbps / wholeStep) * wholeStep; value <= HighKbps; value += wholeStep)
        {
            ticks.Add((int)value);
        }

        return ticks;
    }
}
