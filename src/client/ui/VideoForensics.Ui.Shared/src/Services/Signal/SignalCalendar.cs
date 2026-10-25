namespace VideoForensics.Client.Ui.Shared.Services.Signal;

/// <summary>
/// The readings in a window laid out as a week: 7 weekdays (Monday first, UTC) by 24 hours. Built on the fly from the
/// readings, so it always matches the window. Also gives the hour-of-day and day-of-week profiles, which the grid's
/// margins show as standard deviations.
/// </summary>
public sealed class SignalCalendar
{
    private readonly SignalStats[,] _cells;
    private readonly SignalStats[] _hours;
    private readonly SignalStats[] _days;

    private SignalCalendar(SignalStats[,] cells, SignalStats[] hours, SignalStats[] days)
    {
        _cells = cells;
        _hours = hours;
        _days = days;
    }

    /// <summary>Monday is 0 and Sunday is 6.</summary>
    public static int DayIndex(DateTime utc)
    {
        return ((int)utc.DayOfWeek + 6) % 7;
    }

    /// <summary>Builds the grid from the readings inside the window. Readings without a signal are left out.</summary>
    public static SignalCalendar Compute(IEnumerable<SignalSample> samples, DateTime from, DateTime to)
    {
        ArgumentNullException.ThrowIfNull(samples);

        List<SignalSample> inWindow = samples
            .Where(s => s.RssiDbm is not null && s.CapturedAtUtc >= from && s.CapturedAtUtc <= to)
            .ToList();

        var cells = new SignalStats[7, 24];
        for (int day = 0; day < 7; day++)
        {
            for (int hour = 0; hour < 24; hour++)
            {
                int d = day;
                int h = hour;
                cells[day, hour] = SignalStats.Compute(inWindow.Where(s => DayIndex(s.CapturedAtUtc) == d && s.CapturedAtUtc.Hour == h));
            }
        }

        var hours = Enumerable.Range(0, 24)
            .Select(hour => SignalStats.Compute(inWindow.Where(s => s.CapturedAtUtc.Hour == hour)))
            .ToArray();

        var days = Enumerable.Range(0, 7)
            .Select(day => SignalStats.Compute(inWindow.Where(s => DayIndex(s.CapturedAtUtc) == day)))
            .ToArray();

        return new SignalCalendar(cells, hours, days);
    }

    /// <summary>The stats for one weekday (0 = Monday) and hour (0-23).</summary>
    public SignalStats Cell(int day, int hour) => _cells[day, hour];

    /// <summary>The stats for one hour of the day across every day in the window.</summary>
    public SignalStats HourOfDay(int hour) => _hours[hour];

    /// <summary>The stats for one weekday (0 = Monday) across every hour in the window.</summary>
    public SignalStats DayOfWeek(int day) => _days[day];
}
