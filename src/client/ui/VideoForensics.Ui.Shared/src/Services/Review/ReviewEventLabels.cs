using Microsoft.Extensions.Localization;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.Ui.Shared.Services.Review;

/// <summary>
/// The row labels the review Events and Detail tabs share. A row shows the real detection type when its detail says
/// one (Person, Vehicle), and the event's own type otherwise. A motion row with no detection type shows "Motion".
/// </summary>
public static class ReviewEventLabels
{
    /// <summary>The localized label for a provider event type. A type with no label shows its raw string.</summary>
    public static string TypeLabel(IStringLocalizer localizer, string eventType)
    {
        if (string.IsNullOrWhiteSpace(eventType))
        {
            return localizer["EventTypeUnknown"].Value;
        }

        LocalizedString label = localizer["EventType_" + eventType.ToLowerInvariant()];
        return label.ResourceNotFound ? eventType : label.Value;
    }

    /// <summary>
    /// The localized label for a Ring detection type. A type with no label shows its raw string. "motion" and
    /// "other_motion" say nothing the event does not, so they show the event's own type (also localized) instead.
    /// </summary>
    public static string DetectionLabel(IStringLocalizer localizer, string detectionType, string eventType)
    {
        if (detectionType.Equals("motion", StringComparison.OrdinalIgnoreCase) || detectionType.Equals("other_motion", StringComparison.OrdinalIgnoreCase))
        {
            return TypeLabel(localizer, eventType);
        }

        LocalizedString label = localizer["DetectionType_" + detectionType.ToLowerInvariant()];
        return label.ResourceNotFound ? detectionType : label.Value;
    }

    /// <summary>The row's label: the detection type when the detail names one, else the event's own type.</summary>
    public static string RowLabel(IStringLocalizer localizer, Event evt, EventDetail? detail)
    {
        if (detail?.Detection is { DetectionType: { } detectionType } && !string.IsNullOrWhiteSpace(detectionType))
        {
            return DetectionLabel(localizer, detectionType, evt.EventType);
        }

        return TypeLabel(localizer, evt.EventType);
    }
}
