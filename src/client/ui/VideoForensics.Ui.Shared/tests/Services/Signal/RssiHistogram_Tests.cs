using VideoForensics.Client.Ui.Shared.Services.Signal;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Services.Signal;

/// <summary>The RSSI histogram: one bar per reading in the window, taller for a weaker signal, placed by its time.</summary>
public class RssiHistogram_Tests
{
    private static readonly DateTime WindowStart = new(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime WindowEnd = new(2026, 10, 30, 12, 0, 10, DateTimeKind.Utc);

    [Fact]
    public void RssiHistogram_Skips_Readings_With_No_Signal_And_Readings_Outside_The_Window()
    {
        var readings = new[]
        {
            new SignalSample(WindowStart.AddSeconds(1), -60),
            new SignalSample(WindowStart.AddSeconds(2), null),
            new SignalSample(WindowStart.AddSeconds(-5), -60),
            new SignalSample(WindowEnd.AddSeconds(5), -60),
        };

        IReadOnlyList<RssiBar> bars = RssiHistogram.Bars(readings, WindowStart, WindowEnd);

        Assert.Single(bars);
        Assert.Equal(WindowStart.AddSeconds(1), bars[0].CapturedAtUtc);
    }

    [Fact]
    public void RssiHistogram_Places_Each_Bar_By_Its_Time_In_The_Window()
    {
        var readings = new[] { new SignalSample(WindowStart.AddSeconds(5), -60) };

        IReadOnlyList<RssiBar> bars = RssiHistogram.Bars(readings, WindowStart, WindowEnd);

        Assert.Equal(0.5, bars[0].XFraction, precision: 6);
    }

    [Fact]
    public void RssiHistogram_Makes_A_Weaker_Signal_A_Taller_Bar()
    {
        var readings = new[]
        {
            new SignalSample(WindowStart.AddSeconds(1), -40),
            new SignalSample(WindowStart.AddSeconds(2), -85),
        };

        IReadOnlyList<RssiBar> bars = RssiHistogram.Bars(readings, WindowStart, WindowEnd);

        Assert.True(bars[1].HeightFraction > bars[0].HeightFraction);
        Assert.InRange(bars[0].HeightFraction, 0, 1);
        Assert.InRange(bars[1].HeightFraction, 0, 1);
    }
}
