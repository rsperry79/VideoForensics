using System.Globalization;

namespace VideoForensics.Ui.Shared.Services.Time;

/// <summary>
/// The zone a signed-in user's times are displayed in. Stored times are UTC; this converts them for display.
/// </summary>
/// <remarks>
/// The Blazor Server host runs on the server, so its machine zone is not the user's. The host publishes the browser's
/// zone (see <c>UserTimeZoneHost</c>) and until that arrives the zone is UTC, so a time is never shown in the wrong zone.
/// </remarks>
public sealed record UserTimeZone(TimeZoneInfo Zone)
{
    /// <summary>Zone used before the browser's zone is known.</summary>
    public static UserTimeZone Unavailable { get; } = new(TimeZoneInfo.Utc);

    /// <summary>Returns the instant as a UTC-kind value. Unspecified kinds are treated as UTC; local kinds go through UTC first.</summary>
    public static DateTime AsUtc(DateTime value)
    {
        // Local-kind values carry the machine offset; normalise to UTC before converting to the user's zone.
        return value.Kind == DateTimeKind.Local
            ? value.ToUniversalTime()
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }

    /// <summary>Converts a stored instant into this zone.</summary>
    public DateTime ToLocal(DateTime value)
    {
        return TimeZoneInfo.ConvertTimeFromUtc(AsUtc(value), Zone);
    }

    /// <summary>Formats a stored instant in this zone with the given format string, using the current culture.</summary>
    public string Format(DateTime value, string? format)
    {
        return ToLocal(value).ToString(format, CultureInfo.CurrentCulture);
    }
}
