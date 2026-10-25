using VideoForensics.Client.Ui.Shared.Services.Signal;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>
/// Chart overlays: no-signal readings, low-signal readings, and gaps where at least two checks were missed.
/// Each reading covers the period up to the next check, capped at one expected interval.
/// </summary>
public class SignalPeriodTests
{
    private static readonly DateTime T0 = new(2026, 10, 24, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(15);

    [Fact]
    public void Periods_MarksANoSignalReadingUpToTheNextCheck()
    {
        var samples = new[]
        {
            new SignalSample(T0, null),
            new SignalSample(T0.AddMinutes(15), -50),
            new SignalSample(T0.AddMinutes(30), -50),
            new SignalSample(T0.AddMinutes(45), -50),
        };

        IReadOnlyList<SignalPeriod> periods = SignalChartModel.Periods(samples, T0, T0.AddHours(1), Interval);

        SignalPeriod period = Assert.Single(periods);
        Assert.Equal(SignalPeriodKind.NoSignal, period.Kind);
        Assert.Equal(T0, period.StartUtc);
        Assert.Equal(T0.AddMinutes(15), period.EndUtc);
    }

    [Fact]
    public void Periods_MarksLowSignalButNotTheWeakThresholdItself()
    {
        var samples = new[]
        {
            new SignalSample(T0, -80),
            new SignalSample(T0.AddMinutes(15), -75),
            new SignalSample(T0.AddMinutes(30), -40),
            new SignalSample(T0.AddMinutes(45), -40),
        };

        IReadOnlyList<SignalPeriod> periods = SignalChartModel.Periods(samples, T0, T0.AddHours(1), Interval);

        SignalPeriod period = Assert.Single(periods);
        Assert.Equal(SignalPeriodKind.Weak, period.Kind);
        Assert.Equal(T0, period.StartUtc);
        Assert.Equal(T0.AddMinutes(15), period.EndUtc);
    }

    [Fact]
    public void Periods_IgnoresASingleMissedCheck()
    {
        // 30 minutes between readings is one missed check, which is not flagged.
        var samples = new[]
        {
            new SignalSample(T0, -50),
            new SignalSample(T0.AddMinutes(30), -50),
        };

        IReadOnlyList<SignalPeriod> periods = SignalChartModel.Periods(samples, T0, T0.AddMinutes(30), Interval);

        Assert.Empty(periods);
    }

    [Fact]
    public void Periods_FlagsTwoOrMoreMissedChecksAfterTheFirstMissedCheck()
    {
        // 45 minutes between readings is two missed checks: the gap after the first expected check is flagged.
        var samples = new[]
        {
            new SignalSample(T0, -50),
            new SignalSample(T0.AddMinutes(45), -50),
        };

        IReadOnlyList<SignalPeriod> periods = SignalChartModel.Periods(samples, T0, T0.AddMinutes(45), Interval);

        SignalPeriod period = Assert.Single(periods);
        Assert.Equal(SignalPeriodKind.MissedChecks, period.Kind);
        Assert.Equal(T0.AddMinutes(15), period.StartUtc);
        Assert.Equal(T0.AddMinutes(45), period.EndUtc);
    }

    [Fact]
    public void Periods_FlagsAGapAfterTheLastReadingUpToTheWindowEnd()
    {
        var samples = new[] { new SignalSample(T0, -50) };

        IReadOnlyList<SignalPeriod> periods = SignalChartModel.Periods(samples, T0, T0.AddHours(1), Interval);

        SignalPeriod period = Assert.Single(periods);
        Assert.Equal(SignalPeriodKind.MissedChecks, period.Kind);
        Assert.Equal(T0.AddMinutes(15), period.StartUtc);
        Assert.Equal(T0.AddHours(1), period.EndUtc);
    }

    [Fact]
    public void Periods_MergesAdjacentPeriodsOfTheSameKind()
    {
        var samples = new[]
        {
            new SignalSample(T0, null),
            new SignalSample(T0.AddMinutes(15), null),
            new SignalSample(T0.AddMinutes(30), -50),
        };

        IReadOnlyList<SignalPeriod> periods = SignalChartModel.Periods(samples, T0, T0.AddMinutes(45), Interval);

        SignalPeriod period = Assert.Single(periods);
        Assert.Equal(SignalPeriodKind.NoSignal, period.Kind);
        Assert.Equal(T0, period.StartUtc);
        Assert.Equal(T0.AddMinutes(30), period.EndUtc);
    }

    [Fact]
    public void Periods_IgnoresReadingsOutsideTheWindow()
    {
        var samples = new[] { new SignalSample(T0.AddHours(-1), null) };

        IReadOnlyList<SignalPeriod> periods = SignalChartModel.Periods(samples, T0, T0.AddMinutes(15), Interval);

        Assert.Empty(periods);
    }

    [Fact]
    public void Periods_ReturnsNothingWithoutReadings()
    {
        IReadOnlyList<SignalPeriod> periods = SignalChartModel.Periods([], T0, T0.AddHours(1), Interval);

        Assert.Empty(periods);
    }
}
