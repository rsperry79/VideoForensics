using VideoForensics.Client.Ui.Shared.Services.Signal;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>Signal history stats: readings without a value are ignored, and "weak" means worse than -75 dBm.</summary>
public class SignalStatsTests
{
    private static readonly DateTime T0 = new(2026, 10, 23, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Compute_ReportsMinMaxAverageAndLatest()
    {
        var samples = new[]
        {
            new SignalSample(T0, -50),
            new SignalSample(T0.AddMinutes(15), -70),
            new SignalSample(T0.AddMinutes(30), -60),
        };

        SignalStats stats = SignalStats.Compute(samples);

        Assert.Equal(3, stats.Count);
        Assert.Equal(-70, stats.Min);
        Assert.Equal(-50, stats.Max);
        Assert.Equal(-60.0, stats.Average, precision: 6);
        Assert.Equal(-60, stats.Latest);
    }

    [Fact]
    public void Compute_IgnoresReadingsWithoutASignal()
    {
        var samples = new[]
        {
            new SignalSample(T0, -55),
            new SignalSample(T0.AddMinutes(15), null),
        };

        SignalStats stats = SignalStats.Compute(samples);

        Assert.Equal(1, stats.Count);
        Assert.Equal(-55, stats.Latest);
    }

    [Fact]
    public void Compute_CountsWeakReadingsBelowMinus75()
    {
        var samples = new[]
        {
            new SignalSample(T0, -80),
            new SignalSample(T0.AddMinutes(15), -75),
            new SignalSample(T0.AddMinutes(30), -40),
            new SignalSample(T0.AddMinutes(45), -90),
        };

        SignalStats stats = SignalStats.Compute(samples);

        // -75 is not weaker than -75; only -80 and -90 are.
        Assert.Equal(50.0, stats.PercentWeak, precision: 6);
    }

    [Fact]
    public void Compute_NoReadings_ReturnsEmptyStats()
    {
        SignalStats stats = SignalStats.Compute([]);

        Assert.Equal(0, stats.Count);
        Assert.Null(stats.Latest);
        Assert.Equal(0, stats.PercentWeak);
    }
}
