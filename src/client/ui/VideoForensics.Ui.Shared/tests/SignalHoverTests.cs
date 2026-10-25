using VideoForensics.Client.Ui.Shared.Services.Signal;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>Hover support: the time under the pointer, the reading closest to it, and how the tooltip time is written.</summary>
public class SignalHoverTests
{
    private static readonly DateTime From = new(2026, 10, 24, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime To = new(2026, 10, 25, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void TimeAt_MapsTheFractionAcrossTheWindow()
    {
        Assert.Equal(From, SignalChartModel.TimeAt(0, From, To));
        Assert.Equal(From.AddHours(12), SignalChartModel.TimeAt(0.5, From, To));
        Assert.Equal(To, SignalChartModel.TimeAt(1, From, To));
    }

    [Fact]
    public void NearestReading_ReturnsTheClosestReadingInTime()
    {
        var readings = new[]
        {
            new SignalSample(From.AddHours(1), -50),
            new SignalSample(From.AddHours(2), -70),
            new SignalSample(From.AddHours(5), -60),
        };

        SignalSample? nearest = SignalChartModel.NearestReading(readings, From.AddHours(2).AddMinutes(20));

        Assert.NotNull(nearest);
        Assert.Equal(-70, nearest!.RssiDbm);
    }

    [Fact]
    public void NearestReading_ReturnsNullWithoutReadings()
    {
        Assert.Null(SignalChartModel.NearestReading([], From));
    }

    [Fact]
    public void FormatTooltipTime_WritesTheDateAndUtcClockTime()
    {
        var at = new DateTime(2026, 10, 24, 12, 30, 0, DateTimeKind.Utc);

        Assert.Equal("Oct 24, 12:30 UTC", SignalChartModel.FormatTooltipTime(at));
    }
}
