using VideoForensics.Client.Hosting.Signal;
using VideoForensics.Client.Ui.Shared.Services.Signal;

namespace VideoForensics.Client.Hosting.Tests.Signal;

/// <summary>The bit rate analysis survives the trip to the wire DTOs and back.</summary>
public class BitrateAnalysisMappingTests
{
    private static readonly DateTime From = new(2026, 10, 19, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime To = From.AddDays(7);

    [Fact]
    public void RoundTrip_PreservesEveryFigure()
    {
        BitrateSample[] readings =
        [
            new(From.AddHours(9), 1_000, "Day", false),
            new(From.AddHours(10), 4_000, null, true, Guid.NewGuid(), Guid.NewGuid()),
            new(From.AddHours(11), 2_500, "Ir", false),
        ];
        SignalSample[] samples = [.. readings.Select(r => r.ToSignalSample())];
        var calendar = SignalCalendar.Compute(samples, From, To);
        var device = new BitrateDeviceAnalysis(
            Guid.NewGuid(),
            "Front door",
            "Ring",
            "1234",
            SignalStats.Compute(samples),
            SignalStats.Compute(samples),
            readings,
            [new SignalDeviationWindow(From.AddHours(10), SignalDeviationLevel.OneSigma, 1, 4_000)],
            calendar,
            SignalBuckets.Compute(samples, From, To, SignalBucket.Hour),
            SignalBuckets.Compute(samples, From, To, SignalBucket.Day),
            SignalBuckets.Compute(samples, From, To, SignalBucket.Week));

        BitrateAnalysis back = new BitrateAnalysis([device]).ToDto().ToDomain();

        BitrateDeviceAnalysis restored = Assert.Single(back.Devices);
        Assert.Equal(device.DeviceId, restored.DeviceId);
        Assert.Equal(device.Name, restored.Name);
        Assert.Equal(device.ProviderName, restored.ProviderName);
        Assert.Equal(device.ProviderDeviceId, restored.ProviderDeviceId);
        Assert.Equal(device.AllTime, restored.AllTime);
        Assert.Equal(device.PeriodStats, restored.PeriodStats);
        Assert.Equal(device.Readings, restored.Readings);
        Assert.Equal(device.Deviations, restored.Deviations);
        Assert.Equal(device.Hourly, restored.Hourly);
        Assert.Equal(device.Daily, restored.Daily);
        Assert.Equal(device.Weekly, restored.Weekly);
        for (int day = 0; day < SignalCalendar.Days; day++)
        {
            Assert.Equal(calendar.DayOfWeek(day), restored.Calendar.DayOfWeek(day));
            for (int hour = 0; hour < SignalCalendar.Hours; hour++)
            {
                Assert.Equal(calendar.Cell(day, hour), restored.Calendar.Cell(day, hour));
            }
        }

        for (int hour = 0; hour < SignalCalendar.Hours; hour++)
        {
            Assert.Equal(calendar.HourOfDay(hour), restored.Calendar.HourOfDay(hour));
        }
    }

    [Fact]
    public void ToSignalSample_CarriesKbpsAsTheValue()
    {
        var sample = new BitrateSample(From, 1_234, "Day", true).ToSignalSample();

        Assert.Equal(new SignalSample(From, 1_234), sample);
    }
}
