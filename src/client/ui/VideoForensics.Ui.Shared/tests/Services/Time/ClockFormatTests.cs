using System.Globalization;

using VideoForensics.Client.Ui.Shared.Services.Time;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Services.Time;

/// <summary>Rewrites a display format so the clock shows 12-hour or 24-hour time, without changing the date.</summary>
public class ClockFormat_Apply_Tests
{
    private static readonly CultureInfo EnUs = CultureInfo.GetCultureInfo("en-US");

    [Fact]
    public void Apply_LocalePreference_LeavesTheFormatUnchanged()
    {
        Assert.Equal("HH:mm", ClockFormat.Apply("HH:mm", TimeFormatPreference.Locale, EnUs));
    }

    [Fact]
    public void Apply_TwelveHour_TurnsCustomHourTokenIntoAmPm()
    {
        Assert.Equal("hh:mm tt", ClockFormat.Apply("HH:mm", TimeFormatPreference.TwelveHour, EnUs));
    }

    [Fact]
    public void Apply_TwelveHour_KeepsTheDatePartOfACustomFormat()
    {
        Assert.Equal("yyyy-MM-dd hh:mm:ss tt", ClockFormat.Apply("yyyy-MM-dd HH:mm:ss", TimeFormatPreference.TwelveHour, EnUs));
    }

    [Fact]
    public void Apply_TwentyFourHour_TurnsA12HourFormatIntoHours()
    {
        Assert.Equal("HH:mm", ClockFormat.Apply("hh:mm tt", TimeFormatPreference.TwentyFourHour, EnUs));
    }

    [Fact]
    public void Apply_TwelveHour_GeneralDateTime_UsesTheCultureShortDate()
    {
        Assert.Equal("M/d/yyyy h:mm tt", ClockFormat.Apply("g", TimeFormatPreference.TwelveHour, EnUs));
    }

    [Fact]
    public void Apply_TwentyFourHour_GeneralDateTime_UsesTheCultureShortDate()
    {
        Assert.Equal("M/d/yyyy HH:mm", ClockFormat.Apply("g", TimeFormatPreference.TwentyFourHour, EnUs));
    }

    [Fact]
    public void Apply_TwentyFourHour_NullFormat_UsesTheGeneralDateTimeShape()
    {
        Assert.Equal("M/d/yyyy HH:mm", ClockFormat.Apply(null, TimeFormatPreference.TwentyFourHour, EnUs));
    }

    [Fact]
    public void Apply_ShortTime_IsTheTimeOnly()
    {
        Assert.Equal("h:mm tt", ClockFormat.Apply("t", TimeFormatPreference.TwelveHour, EnUs));
        Assert.Equal("HH:mm", ClockFormat.Apply("t", TimeFormatPreference.TwentyFourHour, EnUs));
    }

    [Fact]
    public void Apply_FormatWithoutAnHourToken_IsUnchanged()
    {
        Assert.Equal("MMM d", ClockFormat.Apply("MMM d", TimeFormatPreference.TwelveHour, EnUs));
        Assert.Equal("u", ClockFormat.Apply("u", TimeFormatPreference.TwelveHour, EnUs));
    }
}

/// <summary>A zone's display shows its clock in the operator's chosen 12-hour or 24-hour format.</summary>
public class UserTimeZone_Format_Clock_Tests
{
    private static readonly TimeZoneInfo Plus9 = TimeZoneInfo.CreateCustomTimeZone("test-plus9", TimeSpan.FromHours(9), "test-plus9", "test-plus9");
    private static readonly DateTime Instant = new(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void Format_TwentyFourHour_ShowsTheHourOnTheTwentyFourHourClock()
    {
        var zone = new UserTimeZone(Plus9, TimeFormatPreference.TwentyFourHour);

        Assert.Equal("19:30", zone.Format(Instant, "HH:mm"));
    }

    [Fact]
    public void Format_TwelveHour_ShowsTheHourOnTheTwelveHourClock()
    {
        var zone = new UserTimeZone(Plus9, TimeFormatPreference.TwelveHour);

        Assert.StartsWith("07:30", zone.Format(Instant, "HH:mm"), StringComparison.Ordinal);
    }

    [Fact]
    public void Format_DefaultClock_KeepsTheFormatAsGiven()
    {
        var zone = new UserTimeZone(Plus9);

        Assert.Equal("19:30", zone.Format(Instant, "HH:mm"));
    }
}
