using System.Globalization;

namespace VideoForensics.Client.Ui.Shared.Services.Time;

/// <summary>Which clock a time is shown on. <see cref="Locale"/> keeps whatever the culture's own pattern uses.</summary>
public enum TimeFormatPreference
{
    Locale,
    TwelveHour,
    TwentyFourHour,
}

/// <summary>
/// Rewrites a .NET display format so its clock matches a <see cref="TimeFormatPreference"/>. The date part is left
/// alone, so one rule covers every page: a date keeps its culture pattern and only the hour changes.
/// </summary>
public static class ClockFormat
{
    /// <summary>
    /// Returns the format to hand to <c>ToString</c>. <see cref="TimeFormatPreference.Locale"/> returns the input
    /// unchanged. Standard formats (<c>g</c>, <c>G</c>, <c>f</c>, <c>F</c>, <c>t</c>, <c>T</c>, and null) are expanded
    /// to explicit patterns because the culture's own clock cannot be switched without rewriting them.
    /// </summary>
    public static string? Apply(string? format, TimeFormatPreference preference, CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        if (preference == TimeFormatPreference.Locale)
        {
            return format;
        }

        bool twelveHour = preference == TimeFormatPreference.TwelveHour;
        DateTimeFormatInfo info = culture.DateTimeFormat;
        return format switch
        {
            null or "g" => $"{info.ShortDatePattern} {ShortTime(twelveHour)}",
            "G" => $"{info.ShortDatePattern} {LongTime(twelveHour)}",
            "f" => $"{info.LongDatePattern} {ShortTime(twelveHour)}",
            "F" => $"{info.LongDatePattern} {LongTime(twelveHour)}",
            "t" => ShortTime(twelveHour),
            "T" => LongTime(twelveHour),
            _ => RewriteHour(format, twelveHour),
        };
    }

    private static string ShortTime(bool twelveHour) => twelveHour ? "h:mm tt" : "HH:mm";

    private static string LongTime(bool twelveHour) => twelveHour ? "h:mm:ss tt" : "HH:mm:ss";

    /// <summary>
    /// Rewrites a custom format's hour tokens. The "tt" designator is added for 12-hour and removed for 24-hour, so the
    /// clock is never shown with a stray AM/PM or a missing one. A format with no hour token is returned as it is.
    /// </summary>
    private static string RewriteHour(string format, bool twelveHour)
    {
        if (format.IndexOfAny(['H', 'h']) < 0)
        {
            return format;
        }

        if (twelveHour)
        {
            string twelve = format.Replace("HH", "hh", StringComparison.Ordinal).Replace("H", "h", StringComparison.Ordinal);
            return twelve.EndsWith(" tt", StringComparison.Ordinal) ? twelve : twelve + " tt";
        }

        string twentyFour = format.Replace(" tt", string.Empty, StringComparison.Ordinal).Replace("hh", "HH", StringComparison.Ordinal);
        return twentyFour.Replace("h", "HH", StringComparison.Ordinal);
    }
}
