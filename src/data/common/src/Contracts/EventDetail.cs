using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>
    /// The detection rows behind one event, gathered for the review page's Detail tab.
    /// </summary>
    /// <param name="EventId">The event these rows belong to.</param>
    /// <param name="Detection">The event's detection summary, or null when the event has no detection row.</param>
    /// <param name="Occurrences">Detection-type occurrences, oldest first.</param>
    /// <param name="Zones">Zones the detection fired in.</param>
    /// <param name="People">People recognised in the event.</param>
    /// <param name="SecurityAlerts">Security alerts raised for the event.</param>
    public sealed record EventDetail(
        Guid EventId,
        EventDetection? Detection,
        IReadOnlyList<EventDetectionTypeOccurrence> Occurrences,
        IReadOnlyList<EventDetectionZone> Zones,
        IReadOnlyList<EventDetectedPerson> People,
        IReadOnlyList<EventSecurityAlert> SecurityAlerts);
}
