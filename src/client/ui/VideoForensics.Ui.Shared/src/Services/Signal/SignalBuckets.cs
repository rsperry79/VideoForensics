namespace VideoForensics.Client.Ui.Shared.Services.Signal;

/// <summary>The size of the time groups the signal table is broken into.</summary>
public enum SignalBucket
{
    Hour,
    Day,
    Week,
}

/// <summary>The stats for one time group. Readings without a signal are counted apart, as the stats ignore them.</summary>
public sealed record SignalBucketStats(DateTime StartUtc, SignalStats Stats, int NoSignalCount);

/// <summary>Groups signal readings into hours, days or weeks (weeks start on Monday, UTC).</summary>
public static class SignalBuckets
{
    /// <summary>The start of the group that <paramref name="utc"/> falls in.</summary>
    public static DateTime StartOf(DateTime utc, SignalBucket size)
    {
        return size switch
        {
            SignalBucket.Hour => utc.Date.AddHours(utc.Hour),
            SignalBucket.Day => utc.Date,

            // Monday is day 0 of the week: DayOfWeek is Sunday = 0, so shift Sunday to the end of the week.
            _ => utc.Date.AddDays(-(((int)utc.DayOfWeek + 6) % 7)),
        };
    }

    /// <summary>
    /// The groups that have at least one reading in the window, earliest first. Groups with no readings are left out,
    /// so a quiet day shows as a gap rather than an empty row.
    /// </summary>
    public static IReadOnlyList<SignalBucketStats> Compute(IEnumerable<SignalSample> samples, DateTime from, DateTime to, SignalBucket size)
    {
        ArgumentNullException.ThrowIfNull(samples);

        return samples
            .Where(s => s.CapturedAtUtc >= from && s.CapturedAtUtc <= to)
            .GroupBy(s => StartOf(s.CapturedAtUtc, size))
            .OrderBy(group => group.Key)
            .Select(group => new SignalBucketStats(
                group.Key,
                SignalStats.Compute(group),
                group.Count(s => s.RssiDbm is null)))
            .ToList();
    }
}
