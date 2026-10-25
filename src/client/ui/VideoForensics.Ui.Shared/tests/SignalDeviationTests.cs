using VideoForensics.Client.Ui.Shared.Services.Signal;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>Readings more than one std dev from the camera's mean mark their hour; more than two raise an alert.</summary>
public class SignalDeviationTests
{
    private static readonly DateTime From = new(2026, 10, 24, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime To = new(2026, 10, 25, 0, 0, 0, DateTimeKind.Utc);

    // Baseline: mean -60, sample std dev 10 (so -70 is 1 std dev away, -80 is 2, -90 is 3).
    private static readonly SignalStats Baseline = new(Count: 10, Min: -80, Max: -40, Average: -60, Latest: -60, PercentWeak: 0, StdDev: 10);

    [Fact]
    public void Classify_IsStrictlyMoreThanOneOrTwoStdDevs()
    {
        Assert.Equal(SignalDeviationLevel.None, SignalDeviation.Classify(-70, -60, 10)); // exactly 1 std dev is not more than 1
        Assert.Equal(SignalDeviationLevel.OneSigma, SignalDeviation.Classify(-75, -60, 10));
        Assert.Equal(SignalDeviationLevel.OneSigma, SignalDeviation.Classify(-80, -60, 10)); // exactly 2 is more than 1 but not more than 2
    }

    [Fact]
    public void Classify_TreatsMoreThanTwoStdDevsAsTheAlertLevel()
    {
        Assert.Equal(SignalDeviationLevel.TwoSigma, SignalDeviation.Classify(-85, -60, 10));
        Assert.Equal(SignalDeviationLevel.TwoSigma, SignalDeviation.Classify(-40, -60, 5)); // 4 std devs above the mean
    }

    [Fact]
    public void Classify_IsNoneWhenThereIsNoSpread()
    {
        Assert.Equal(SignalDeviationLevel.None, SignalDeviation.Classify(-90, -60, 0));
    }

    [Fact]
    public void Windows_MarksEachHourWithAReadingMoreThanOneStdDevAway()
    {
        var samples = new[]
        {
            new SignalSample(new DateTime(2026, 10, 24, 9, 5, 0, DateTimeKind.Utc), -70),  // exactly 1 std dev: not flagged
            new SignalSample(new DateTime(2026, 10, 24, 9, 10, 0, DateTimeKind.Utc), -75), // 1.5 std devs
            new SignalSample(new DateTime(2026, 10, 24, 9, 20, 0, DateTimeKind.Utc), -85), // 2.5 std devs
            new SignalSample(new DateTime(2026, 10, 24, 10, 5, 0, DateTimeKind.Utc), -55), // 0.5 std devs: not flagged
            new SignalSample(new DateTime(2026, 10, 24, 11, 0, 0, DateTimeKind.Utc), -90), // 3 std devs
        };

        IReadOnlyList<SignalDeviationWindow> windows = SignalDeviation.Windows(samples, From, To, Baseline);

        Assert.Equal(2, windows.Count);
        Assert.Equal(new DateTime(2026, 10, 24, 9, 0, 0, DateTimeKind.Utc), windows[0].HourStartUtc);
        Assert.Equal(SignalDeviationLevel.TwoSigma, windows[0].Level);
        Assert.Equal(2, windows[0].FlaggedReadings);
        Assert.Equal(-85, windows[0].WorstRssiDbm);
        Assert.Equal(new DateTime(2026, 10, 24, 11, 0, 0, DateTimeKind.Utc), windows[1].HourStartUtc);
    }

    [Fact]
    public void Windows_IsEmptyWithoutABaselineSpread()
    {
        var samples = new[] { new SignalSample(From.AddHours(1), -95) };
        var flat = Baseline with { StdDev = null };

        Assert.Empty(SignalDeviation.Windows(samples, From, To, flat));
    }

    [Fact]
    public void DedupeKey_IsStablePerCameraAndHour()
    {
        var device = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var hour = new DateTime(2026, 10, 24, 9, 0, 0, DateTimeKind.Utc);

        Assert.Equal("sig-dev:11111111222233334444555555555555:2026102409", SignalDeviation.DedupeKey(device, hour));
    }
}
