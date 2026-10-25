namespace VideoForensics.Client.Ui.Shared.Services.Signal;

/// <summary>
/// The readings in a window laid out as a week: 7 weekdays (Monday first, UTC) by 24 hours. Computed from the readings,
/// so it always matches the window, and rebuilt from transported stats on a client. Also gives the hour-of-day and
/// day-of-week profiles, which the grid's margins show as standard deviations.
/// </summary>
public sealed class SignalCalendar
{
    /// <summary>Weekdays in the grid (Monday to Sunday).</summary>
    public const int Days = 7;

    /// <summary>Hours in each day.</summary>
    public const int Hours = 24;

    private readonly IReadOnlyList<SignalStats> _cells;
    private readonly IReadOnlyList<SignalStats> _hourOfDay;
    private readonly IReadOnlyList<SignalStats> _dayOfWeek;

    /// <summary>Builds the grid from precomputed stats. Lists must hold 168 cells (day-major, Monday first), 24 hours and 7 days.</summary>
    public SignalCalendar(IReadOnlyList<SignalStats> cells, IReadOnlyList<SignalStats> hourOfDay, IReadOnlyList<SignalStats> dayOfWeek)
    {
        ArgumentNullException.ThrowIfNull(cells);
        ArgumentNullException.ThrowIfNull(hourOfDay);
        ArgumentNullException.ThrowIfNull(dayOfWeek);
        if (cells.Count != Days * Hours || hourOfDay.Count != Hours || dayOfWeek.Count != Days)
        {
            throw new ArgumentException("A calendar needs 168 cells, 24 hour profiles and 7 day profiles.");
        }

        _cells = cells;
        _hourOfDay = hourOfDay;
        _dayOfWeek = dayOfWeek;
    }

    /// <summary>All 168 cells, day-major: index is day * 24 + hour.</summary>
    public IReadOnlyList<SignalStats> CellList => _cells;

    /// <summary>The 24 hour-of-day profiles.</summary>
    public IReadOnlyList<SignalStats> HourOfDayList => _hourOfDay;

    /// <summary>The 7 day-of-week profiles.</summary>
    public IReadOnlyList<SignalStats> DayOfWeekList => _dayOfWeek;

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

        var cells = new List<SignalStats>(Days * Hours);
        for (int day = 0; day < Days; day++)
        {
            for (int hour = 0; hour < Hours; hour++)
            {
                int d = day;
                int h = hour;
                cells.Add(SignalStats.Compute(inWindow.Where(s => DayIndex(s.CapturedAtUtc) == d && s.CapturedAtUtc.Hour == h)));
            }
        }

        var hours = Enumerable.Range(0, Hours)
            .Select(hour => SignalStats.Compute(inWindow.Where(s => s.CapturedAtUtc.Hour == hour)))
            .ToList();

        var days = Enumerable.Range(0, Days)
            .Select(day => SignalStats.Compute(inWindow.Where(s => DayIndex(s.CapturedAtUtc) == day)))
            .ToList();

        return new SignalCalendar(cells, hours, days);
    }

    /// <summary>The stats for one weekday (0 = Monday) and hour (0-23).</summary>
    public SignalStats Cell(int day, int hour) => _cells[(day * Hours) + hour];

    /// <summary>The stats for one hour of the day across every day in the window.</summary>
    public SignalStats HourOfDay(int hour) => _hourOfDay[hour];

    /// <summary>The stats for one weekday (0 = Monday) across every hour in the window.</summary>
    public SignalStats DayOfWeek(int day) => _dayOfWeek[day];
}
