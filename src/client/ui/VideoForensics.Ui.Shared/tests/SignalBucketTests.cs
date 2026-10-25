using VideoForensics.Client.Ui.Shared.Services.Signal;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>Signal readings grouped by hour, day or week, with the stats for each group.</summary>
public class SignalBucketTests
{
    // 2026-10-19 is a Monday, 2026-10-25 a Sunday.
    private static readonly DateTime From = new(2026, 10, 19, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime To = new(2026, 10, 26, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void StartOf_TruncatesToTheHourDayOrMondayWeek()
    {
        var at = new DateTime(2026, 10, 22, 14, 37, 0, DateTimeKind.Utc); // a Thursday

        Assert.Equal(new DateTime(2026, 10, 22, 14, 0, 0, DateTimeKind.Utc), SignalBuckets.StartOf(at, SignalBucket.Hour));
        Assert.Equal(new DateTime(2026, 10, 22, 0, 0, 0, DateTimeKind.Utc), SignalBuckets.StartOf(at, SignalBucket.Day));
        Assert.Equal(new DateTime(2026, 10, 19, 0, 0, 0, DateTimeKind.Utc), SignalBuckets.StartOf(at, SignalBucket.Week));
    }

    [Fact]
    public void StartOf_TheWeekStartsOnMondayEvenOnASunday()
    {
        var sunday = new DateTime(2026, 10, 25, 23, 0, 0, DateTimeKind.Utc);

        Assert.Equal(new DateTime(2026, 10, 19, 0, 0, 0, DateTimeKind.Utc), SignalBuckets.StartOf(sunday, SignalBucket.Week));
    }

    [Fact]
    public void Compute_GroupsReadingsByDayWithTheStatsForEachDay()
    {
        var samples = new[]
        {
            new SignalSample(new DateTime(2026, 10, 20, 1, 0, 0, DateTimeKind.Utc), -50),
            new SignalSample(new DateTime(2026, 10, 20, 9, 0, 0, DateTimeKind.Utc), -70),
            new SignalSample(new DateTime(2026, 10, 21, 9, 0, 0, DateTimeKind.Utc), -60),
        };

        IReadOnlyList<SignalBucketStats> buckets = SignalBuckets.Compute(samples, From, To, SignalBucket.Day);

        Assert.Equal(2, buckets.Count);
        Assert.Equal(new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc), buckets[0].StartUtc);
        Assert.Equal(2, buckets[0].Stats.Count);
        Assert.Equal(-70, buckets[0].Stats.Min);
        Assert.Equal(-50, buckets[0].Stats.Max);
        Assert.Equal(20, buckets[0].Stats.Delta);
        Assert.Equal(-60.0, buckets[0].Stats.Average, precision: 6);
    }

    [Fact]
    public void Compute_CountsNoSignalReadingsSeparatelyFromTheStats()
    {
        var samples = new[]
        {
            new SignalSample(new DateTime(2026, 10, 20, 1, 0, 0, DateTimeKind.Utc), null),
            new SignalSample(new DateTime(2026, 10, 20, 2, 0, 0, DateTimeKind.Utc), -55),
        };

        SignalBucketStats bucket = Assert.Single(SignalBuckets.Compute(samples, From, To, SignalBucket.Day));

        Assert.Equal(1, bucket.Stats.Count);
        Assert.Equal(1, bucket.NoSignalCount);
    }

    [Fact]
    public void Compute_LeavesOutBucketsWithNoReadings()
    {
        var samples = new[]
        {
            new SignalSample(new DateTime(2026, 10, 20, 1, 0, 0, DateTimeKind.Utc), -50),
            new SignalSample(new DateTime(2026, 10, 24, 1, 0, 0, DateTimeKind.Utc), -50),
        };

        IReadOnlyList<SignalBucketStats> buckets = SignalBuckets.Compute(samples, From, To, SignalBucket.Day);

        Assert.Equal(2, buckets.Count);
    }

    [Fact]
    public void Compute_IgnoresReadingsOutsideTheWindow()
    {
        var samples = new[] { new SignalSample(From.AddDays(-2), -50) };

        Assert.Empty(SignalBuckets.Compute(samples, From, To, SignalBucket.Week));
    }

    [Fact]
    public void Compute_GroupsByHour()
    {
        var samples = new[]
        {
            new SignalSample(new DateTime(2026, 10, 20, 9, 5, 0, DateTimeKind.Utc), -50),
            new SignalSample(new DateTime(2026, 10, 20, 9, 55, 0, DateTimeKind.Utc), -60),
            new SignalSample(new DateTime(2026, 10, 20, 10, 5, 0, DateTimeKind.Utc), -70),
        };

        IReadOnlyList<SignalBucketStats> buckets = SignalBuckets.Compute(samples, From, To, SignalBucket.Hour);

        Assert.Equal(2, buckets.Count);
        Assert.Equal(2, buckets[0].Stats.Count);
    }
}
