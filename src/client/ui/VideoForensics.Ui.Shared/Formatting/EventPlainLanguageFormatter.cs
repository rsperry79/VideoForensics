using VideoForensics.Api.Contracts;
using Microsoft.Extensions.Localization;

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
    /// <param name="localizer">Localizer used for every user-visible string (resource keys live in SharedResources).</param>
    /// <param name="eventDto">The event to describe.</param>
    /// <param name="deviceName">Friendly name of the device, optional.</param>
    /// <returns>A plain-language description of the event.</returns>
    public static string Describe(IStringLocalizer localizer, EventDto eventDto, string? deviceName = null)
    {
        if (eventDto == null)
            return localizer["PlainEventDetected"].Value;

        var devicePart = !string.IsNullOrWhiteSpace(deviceName)
            ? localizer["PlainDeviceNamed", deviceName].Value
            : localizer["PlainDeviceFallback"].Value;

        return eventDto.EventType.ToLowerInvariant() switch
        {
            "motion" => localizer["PlainEventMotion", devicePart].Value,
            "person" => localizer["PlainEventPerson", devicePart].Value,
            "package" => localizer["PlainEventPackage", devicePart].Value,
            // Unknown types get a generic line: the raw provider type is technical and must never reach victims.
            _ => localizer["PlainEventGeneric", devicePart].Value
        };
    }

    /// <summary>
    /// Describes a jamming incident in plain, non-technical language.
    /// Translates technical signal degradation metrics into user-friendly terms.
    /// </summary>
    /// <param name="localizer">Localizer used for every user-visible string (resource keys live in SharedResources).</param>
    /// <param name="deviceName">Friendly name of the device.</param>
    /// <param name="startUtc">Start time of the jamming incident, in UTC.</param>
    /// <param name="endUtc">End time of the jamming incident, in UTC.</param>
    /// <param name="rssiValue">Signal strength (RSSI) in dBm at time of incident, optional.</param>
    /// <param name="degradationDb">Signal degradation in dB, optional.</param>
    /// <param name="confidence">Confidence level of the jamming detection (Low, Medium, High, Definite), optional.</param>
    /// <returns>A plain-language description of the jamming incident.</returns>
    public static string DescribeJammingIncident(
        IStringLocalizer localizer,
        string? deviceName,
        DateTime startUtc,
        DateTime endUtc,
        int? rssiValue = null,
        double? degradationDb = null,
        string? confidence = null)
    {
        // The "Your " guard is a defensive English-only heuristic for callers that already prefixed the name.
        if (string.IsNullOrWhiteSpace(deviceName))
            deviceName = localizer["PlainDeviceFallback"].Value;
        else if (!deviceName.StartsWith("Your ", StringComparison.OrdinalIgnoreCase))
            deviceName = localizer["PlainDeviceNamed", deviceName].Value;

        var duration = endUtc - startUtc;
        var durationText = FormatDuration(localizer, duration);

        return localizer["PlainJammingBlocked", deviceName, durationText].Value;
    }

    /// <summary>
    /// Formats a time duration into human-readable text.
    /// </summary>
    private static string FormatDuration(IStringLocalizer localizer, TimeSpan duration)
    {
        if (duration.TotalSeconds < 60)
        {
            var seconds = (int)Math.Round(duration.TotalSeconds);
            return seconds == 1 ? localizer["PlainDurationSecond"].Value : localizer["PlainDurationSeconds", seconds].Value;
        }

        if (duration.TotalMinutes < 60)
        {
            var minutes = (int)Math.Round(duration.TotalMinutes);
            return minutes == 1 ? localizer["PlainDurationMinute"].Value : localizer["PlainDurationMinutes", minutes].Value;
        }

        if (duration.TotalHours < 24)
        {
            var hours = duration.Hours;
            var remainingMinutes = duration.Minutes;

            if (remainingMinutes == 0)
                return hours == 1 ? localizer["PlainDurationHour"].Value : localizer["PlainDurationHours", hours].Value;

            return remainingMinutes == 1
                ? localizer["PlainDurationHoursAndMinute", hours].Value
                : localizer["PlainDurationHoursAndMinutes", hours, remainingMinutes].Value;
        }

        var days = (int)Math.Round(duration.TotalDays);
        return days == 1 ? localizer["PlainDurationDay"].Value : localizer["PlainDurationDays", days].Value;
    }
}
