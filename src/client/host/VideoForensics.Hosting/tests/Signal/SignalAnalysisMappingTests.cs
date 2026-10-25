using VideoForensics.Client.Hosting.Signal;
using VideoForensics.Client.Ui.Shared.Services.Signal;
using Xunit;

namespace VideoForensics.Client.Hosting.Tests.Signal;

/// <summary>The analysis survives the trip to the wire DTOs and back, so a remote client shows what the server computed.</summary>
public class SignalAnalysisMappingTests
{
    private static readonly DateTime From = new(2026, 10, 19, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime To = From.AddDays(7);

    [Fact]
    public void RoundTrip_PreservesEveryFigure()
    {
        var samples = new[]
        {
            new SignalSample(From.AddHours(9), -50),
            new SignalSample(From.AddHours(10), null),
            new SignalSample(From.AddHours(11), -80),
        };
        SignalCalendar calendar = SignalCalendar.Compute(samples, From, To);
        var device = new SignalDeviceAnalysis(
            Guid.NewGuid(),
            "Front door",
            "Ring",
            "1234",
            SignalStats.Compute(samples),
            SignalStats.Compute(samples),
            samples,
            [new SignalPeriod(SignalPeriodKind.NoSignal, From.AddHours(10), From.AddHours(11))],
            [new SignalDeviationWindow(From.AddHours(11), SignalDeviationLevel.TwoSigma, 1, -80)],
            calendar,
            SignalBuckets.Compute(samples, From, To, SignalBucket.Hour),
            SignalBuckets.Compute(samples, From, To, SignalBucket.Day),
            SignalBuckets.Compute(samples, From, To, SignalBucket.Week));

        SignalAnalysis back = new SignalAnalysis([device]).ToDto().ToDomain();

        SignalDeviceAnalysis restored = Assert.Single(back.Devices);
        Assert.Equal(device.DeviceId, restored.DeviceId);
        Assert.Equal(device.Name, restored.Name);
        Assert.Equal(device.ProviderName, restored.ProviderName);
        Assert.Equal(device.ProviderDeviceId, restored.ProviderDeviceId);
        Assert.Equal(device.AllTime, restored.AllTime);
        Assert.Equal(device.PeriodStats, restored.PeriodStats);
        Assert.Equal(device.Readings, restored.Readings);
        Assert.Equal(device.Periods, restored.Periods);
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
    }
}
