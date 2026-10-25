namespace VideoForensics.Client.Ui.Shared.Services.Signal;

/// <summary>How far a reading sits from the camera's mean, in std devs.</summary>
public enum SignalDeviationLevel
{
    /// <summary>Within one std dev of the mean, or no spread to measure against.</summary>
    None,

    /// <summary>More than one and at most two std devs from the mean. Marks the hour on the timeline.</summary>
    OneSigma,

    /// <summary>More than two std devs from the mean. Marks the hour and raises an alert.</summary>
    TwoSigma,
}

/// <summary>An hour that holds at least one reading flagged above one std dev.</summary>
/// <param name="HourStartUtc">The start of the hour.</param>
/// <param name="Level">The highest level of any flagged reading in the hour.</param>
/// <param name="FlaggedReadings">How many readings in the hour are flagged.</param>
/// <param name="WorstRssiDbm">The flagged reading furthest from the mean.</param>
public sealed record SignalDeviationWindow(DateTime HourStartUtc, SignalDeviationLevel Level, int FlaggedReadings, int WorstRssiDbm);

/// <summary>
/// Flags readings against the camera's own mean and sample std dev (the all-time figures the stats show). Readings more
/// than one std dev away mark their hour on the timeline; more than two raise an alert.
/// </summary>
public static class SignalDeviation
{
    /// <summary>Std devs from the mean above which a reading is flagged.</summary>
    public const double FlagSigma = 1;

    /// <summary>Std devs from the mean above which a reading raises an alert.</summary>
    public const double AlertSigma = 2;

    /// <summary>Classifies one reading. Without a spread there is nothing to measure against, so nothing is flagged.</summary>
    public static SignalDeviationLevel Classify(double rssiDbm, double mean, double stdDev)
    {
        if (stdDev <= 0)
        {
            return SignalDeviationLevel.None;
        }

        double sigmas = Math.Abs(rssiDbm - mean) / stdDev;
        if (sigmas > AlertSigma)
        {
            return SignalDeviationLevel.TwoSigma;
        }

        return sigmas > FlagSigma ? SignalDeviationLevel.OneSigma : SignalDeviationLevel.None;
    }

    /// <summary>
    /// The hours that hold a flagged reading, earliest first. Readings are measured against the baseline's all-time mean
    /// and std dev. Returns nothing when the baseline has no spread.
    /// </summary>
    public static IReadOnlyList<SignalDeviationWindow> Windows(IEnumerable<SignalSample> samples, DateTime from, DateTime to, SignalStats baseline)
    {
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentNullException.ThrowIfNull(baseline);

        if (baseline.StdDev is not double stdDev || stdDev <= 0)
        {
            return [];
        }

        var flagged = samples
            .Where(s => s.RssiDbm is not null && s.CapturedAtUtc >= from && s.CapturedAtUtc <= to)
            .Select(s => (Sample: s, Level: Classify(s.RssiDbm!.Value, baseline.Average, stdDev)))
            .Where(f => f.Level != SignalDeviationLevel.None)
            .ToList();

        return flagged
            .GroupBy(f => SignalBuckets.StartOf(f.Sample.CapturedAtUtc, SignalBucket.Hour))
            .OrderBy(group => group.Key)
            .Select(group =>
            {
                var worst = group.OrderByDescending(f => Math.Abs(f.Sample.RssiDbm!.Value - baseline.Average)).First();
                return new SignalDeviationWindow(
                    group.Key,
                    (SignalDeviationLevel)group.Max(f => (int)f.Level),
                    group.Count(),
                    worst.Sample.RssiDbm!.Value);
            })
            .ToList();
    }

    /// <summary>
    /// The key that identifies one camera's alert for one hour. Stored in the notice details, it stops the same hour
    /// raising the same alert on every page load.
    /// </summary>
    public static string DedupeKey(Guid deviceId, DateTime hourStartUtc)
    {
        return $"sig-dev:{deviceId:N}:{hourStartUtc:yyyyMMddHH}";
    }
}
