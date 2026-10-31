using VideoForensics.Client.Ui.Shared.Services.Signal;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>The 7-day by 24-hour grid, and the hour-of-day and day-of-week profiles, all calculated from the readings.</summary>
public class SignalCalendarTests
{
    // 2026-10-19 is a Monday, 2026-10-20 a Tuesday.
    private static readonly DateTime From = new(2026, 10, 19, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime To = new(2026, 10, 26, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void DayIndex_CountsFromMonday()
    {
        Assert.Equal(0, SignalCalendar.DayIndex(new DateTime(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc)));
        Assert.Equal(6, SignalCalendar.DayIndex(new DateTime(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void Cell_HoldsTheReadingsForThatWeekdayAndHour()
    {
        SignalCalendar calendar = SignalCalendar.Compute(
            [
                new SignalSample(new DateTime(2026, 10, 19, 9, 0, 0, DateTimeKind.Utc), -50),
                new SignalSample(new DateTime(2026, 10, 19, 9, 30, 0, DateTimeKind.Utc), -70),
                new SignalSample(new DateTime(2026, 10, 20, 9, 0, 0, DateTimeKind.Utc), -60),
            ],
            From,
            To);

        Assert.Equal(2, calendar.Cell(0, 9).Count);
        Assert.Equal(-60.0, calendar.Cell(0, 9).Average, precision: 6);
        Assert.Equal(-60.0, calendar.Cell(1, 9).Average, precision: 6);
        Assert.Equal(0, calendar.Cell(2, 3).Count);
    }

    [Fact]
    public void HourOfDayProfile_IsTheStatsAcrossAllDaysForThatHour()
    {
        SignalCalendar calendar = SignalCalendar.Compute(
            [
                new SignalSample(new DateTime(2026, 10, 19, 9, 0, 0, DateTimeKind.Utc), -50),
                new SignalSample(new DateTime(2026, 10, 20, 9, 0, 0, DateTimeKind.Utc), -70),
                new SignalSample(new DateTime(2026, 10, 21, 9, 0, 0, DateTimeKind.Utc), -60),
            ],
            From,
            To);

        // Values -50, -70, -60: mean -60, sample deviation 10.
        Assert.Equal(3, calendar.HourOfDay(9).Count);
        Assert.Equal(10.0, calendar.HourOfDay(9).StdDev!.Value, precision: 6);
    }

    [Fact]
    public void DayOfWeekProfile_IsTheStatsAcrossAllHoursOfThatWeekday()
    {
        SignalCalendar calendar = SignalCalendar.Compute(
            [
                new SignalSample(new DateTime(2026, 10, 19, 9, 0, 0, DateTimeKind.Utc), -50),
                new SignalSample(new DateTime(2026, 10, 19, 21, 0, 0, DateTimeKind.Utc), -70),
            ],
            From,
            To);

        // Values -50, -70: mean -60, sample deviation sqrt(200) = 14.142...
        Assert.Equal(2, calendar.DayOfWeek(0).Count);
        Assert.Equal(14.1421, calendar.DayOfWeek(0).StdDev!.Value, precision: 4);
    }

    [Fact]
    public void Constructor_RebuildsTheGridFromItsTransportedStats()
    {
        SignalCalendar original = SignalCalendar.Compute(
            [new SignalSample(new DateTime(2026, 10, 20, 9, 0, 0, DateTimeKind.Utc), -60)],
            From,
            To);

        var rebuilt = new SignalCalendar(original.CellList, original.HourOfDayList, original.DayOfWeekList);

        Assert.Equal(-60.0, rebuilt.Cell(1, 9).Average, precision: 6);
        Assert.Equal(original.HourOfDay(9), rebuilt.HourOfDay(9));
    }

    [Fact]
    public void Constructor_RejectsAGridOfTheWrongSize()
    {
        Assert.Throws<ArgumentException>(() => new SignalCalendar([], [], []));
    }

    [Fact]
    public void Compute_IgnoresReadingsOutsideTheWindowAndWithoutASignal()
    {
        SignalCalendar calendar = SignalCalendar.Compute(
            [
                new SignalSample(From.AddDays(-1), -50),
                new SignalSample(new DateTime(2026, 10, 19, 9, 0, 0, DateTimeKind.Utc), null),
            ],
            From,
            To);

        Assert.Equal(0, calendar.Cell(0, 9).Count);
        Assert.Equal(0, calendar.HourOfDay(9).Count);
    }
}
