using VideoForensics.Api.Contracts;

namespace VideoForensics.Ui.Shared.Formatting;

/// <summary>
/// Translates technical event and jamming data into plain, non-technical language
/// suitable for crime-victim end users.
/// </summary>
public static class EventPlainLanguageFormatter
{
    /// <summary>
    /// Describes an event in plain, non-technical language.
    /// </summary>
    /// <param name="eventDto">The event to describe.</param>
    /// <param name="deviceName">Friendly name of the device, optional.</param>
    /// <returns>A plain-language description of the event.</returns>
    public static string Describe(EventDto eventDto, string? deviceName = null)
    {
        if (eventDto == null)
            return "An event was detected.";

        var devicePart = !string.IsNullOrWhiteSpace(deviceName) ? $"Your {deviceName}" : "Your camera";

        return eventDto.EventType.ToLowerInvariant() switch
        {
            "motion" => $"{devicePart} detected motion.",
            "person" => $"{devicePart} detected a person.",
            "package" => $"{devicePart} detected a package.",
            _ => $"{devicePart} detected an event of type '{eventDto.EventType}'."
        };
    }

    /// <summary>
    /// Describes a jamming incident in plain, non-technical language.
    /// Translates technical signal degradation metrics into user-friendly terms.
    /// </summary>
    /// <param name="deviceName">Friendly name of the device.</param>
    /// <param name="startUtc">Start time of the jamming incident, in UTC.</param>
    /// <param name="endUtc">End time of the jamming incident, in UTC.</param>
    /// <param name="rssiValue">Signal strength (RSSI) in dBm at time of incident, optional.</param>
    /// <param name="degradationDb">Signal degradation in dB, optional.</param>
    /// <param name="confidence">Confidence level of the jamming detection (Low, Medium, High, Definite), optional.</param>
    /// <returns>A plain-language description of the jamming incident.</returns>
    public static string DescribeJammingIncident(
        string? deviceName,
        DateTime startUtc,
        DateTime endUtc,
        int? rssiValue = null,
        double? degradationDb = null,
        string? confidence = null)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
            deviceName = "Your camera";
        else if (!deviceName.StartsWith("Your ", StringComparison.OrdinalIgnoreCase))
            deviceName = $"Your {deviceName}";

        var duration = endUtc - startUtc;
        var durationText = FormatDuration(duration);

        return $"{deviceName} was blocked for {durationText}.";
    }

    /// <summary>
    /// Formats a time duration into human-readable text.
    /// </summary>
    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalSeconds < 60)
        {
            var seconds = (int)Math.Round(duration.TotalSeconds);
            return seconds == 1 ? "1 second" : $"{seconds} seconds";
        }

        if (duration.TotalMinutes < 60)
        {
            var minutes = (int)Math.Round(duration.TotalMinutes);
            return minutes == 1 ? "1 minute" : $"{minutes} minutes";
        }

        if (duration.TotalHours < 24)
        {
            var hours = duration.Hours;
            var remainingMinutes = duration.Minutes;

            if (remainingMinutes == 0)
                return hours == 1 ? "1 hour" : $"{hours} hours";

            return remainingMinutes == 1
                ? $"{hours} hours and 1 minute"
                : $"{hours} hours and {remainingMinutes} minutes";
        }

        var days = (int)Math.Round(duration.TotalDays);
        return days == 1 ? "1 day" : $"{days} days";
    }
}
