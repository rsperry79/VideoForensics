namespace VideoForensics.Client.Ui.Shared.Services.Signal;

/// <summary>One health reading's Wi-Fi signal in dBm, at the time it was taken. Null means Ring gave no signal.</summary>
public sealed record SignalSample(DateTime CapturedAtUtc, int? RssiDbm);

/// <summary>Summary of a camera's signal history. Ring reports RSSI (dBm, closer to zero is stronger), not SNR.</summary>
/// <param name="StdDev">Sample standard deviation (n - 1) in dBm. Null with fewer than two readings.</param>
public sealed record SignalStats(int Count, int? Min, int? Max, double Average, int? Latest, double PercentWeak, double? StdDev = null)
{
    /// <summary>
    /// The spread of the readings in dBm: the strongest minus the weakest. Null when there are no readings.
    /// Matches the all-time min and max the chart bars show.
    /// </summary>
    public int? Delta => Max - Min;

    /// <summary>Readings weaker than this count as weak.</summary>
    public const int WeakThresholdDbm = -75;

    /// <summary>Summarises the readings that have a signal value. Readings without one are left out of every figure.</summary>
    public static SignalStats Compute(IEnumerable<SignalSample> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);

        List<SignalSample> withSignal = samples.Where(s => s.RssiDbm is not null).OrderBy(s => s.CapturedAtUtc).ToList();
        if (withSignal.Count == 0)
        {
            return new SignalStats(0, null, null, 0, null, 0);
        }

        var values = withSignal.Select(s => s.RssiDbm!.Value).ToList();
        int weak = values.Count(v => v < WeakThresholdDbm);
        double average = values.Average();
        return new SignalStats(
            Count: values.Count,
            Min: values.Min(),
            Max: values.Max(),
            Average: average,
            Latest: values[^1],
            PercentWeak: 100.0 * weak / values.Count,
            StdDev: SampleStdDev(values, average));
    }

    /// <summary>
    /// Sample standard deviation (divides by n - 1), so a short history is not understated. Needs at least two values;
    /// one reading has no spread to measure.
    /// </summary>
    private static double? SampleStdDev(List<int> values, double average)
    {
        if (values.Count < 2)
        {
            return null;
        }

        double sumOfSquares = values.Sum(v => (v - average) * (v - average));
        return Math.Sqrt(sumOfSquares / (values.Count - 1));
    }
}
