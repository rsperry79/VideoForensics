namespace VideoForensics.Client.Ui.Shared.Services.Signal;

/// <summary>
/// The dBm range the chart's y-axis covers. It is fitted to the displayed readings so small changes stay readable,
/// rather than always spanning the full -100 to -30 dBm range.
/// </summary>
/// <param name="WeakestDbm">The value at the bottom of the plot (the most negative edge).</param>
/// <param name="StrongestDbm">The value at the top of the plot (the edge closest to zero).</param>
public readonly record struct SignalScale(double WeakestDbm, double StrongestDbm)
{
    /// <summary>Fraction of the displayed min-to-max delta added above and below the readings.</summary>
    public const double Padding = 0.15;

    /// <summary>Smallest span the axis will show, so a flat reading still has a readable scale.</summary>
    public const double MinimumSpanDbm = 10;

    /// <summary>The axis used when there are no readings to fit: the full range from -100 to -30 dBm.</summary>
    public static SignalScale Default => new(-100, -30);

    /// <summary>
    /// Fits the axis to a displayed minimum and maximum: 15% of the delta is added on each side. A flat reading
    /// (zero delta) is widened to <see cref="MinimumSpanDbm"/> so it still has a scale.
    /// </summary>
    public static SignalScale Fit(double min, double max)
    {
        double delta = max - min;
        double padding = delta * Padding;
        double low = min - padding;
        double high = max + padding;

        double shortfall = MinimumSpanDbm - (high - low);
        if (shortfall > 0)
        {
            low -= shortfall / 2;
            high += shortfall / 2;
        }

        return new SignalScale(low, high);
    }

    /// <summary>The y coordinate for a dBm value. Stronger signal sits higher; values beyond the scale are clamped.</summary>
    public double MapY(double dbm)
    {
        double clamped = Math.Clamp(dbm, WeakestDbm, StrongestDbm);
        return SignalChartModel.PlotTop + (StrongestDbm - clamped) / (StrongestDbm - WeakestDbm) * SignalChartModel.PlotHeight;
    }

    /// <summary>
    /// Whole-dBm gridline values inside the scale, about five of them. The step is the smallest of 1, 2, 5, 10, 20 or 50
    /// that keeps the count at six or fewer.
    /// </summary>
    public IReadOnlyList<int> Ticks()
    {
        double span = StrongestDbm - WeakestDbm;
        int step = new[] { 1, 2, 5, 10, 20, 50 }.First(s => span / s <= 6);

        int first = (int)Math.Ceiling(WeakestDbm / step) * step;
        var ticks = new List<int>();
        for (int value = first; value <= StrongestDbm; value += step)
        {
            ticks.Add(value);
        }

        return ticks;
    }
}
