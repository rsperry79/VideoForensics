namespace VideoForensics.Client.Ui.Shared.Services.Signal;

/// <summary>One health reading's Wi-Fi signal in dBm, at the time it was taken. Null means Ring gave no signal.</summary>
public sealed record SignalSample(DateTime CapturedAtUtc, int? RssiDbm);

/// <summary>Summary of a camera's signal history. Ring reports RSSI (dBm, closer to zero is stronger), not SNR.</summary>
public sealed record SignalStats(int Count, int? Min, int? Max, double Average, int? Latest, double PercentWeak)
{
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
        return new SignalStats(
            Count: values.Count,
            Min: values.Min(),
            Max: values.Max(),
            Average: values.Average(),
            Latest: values[^1],
            PercentWeak: 100.0 * weak / values.Count);
    }
}
