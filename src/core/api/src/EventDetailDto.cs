using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>
    /// The detection rows behind one event, for the review page's Detail tab. Nullable fields stay null when the source row has no value.
    /// </summary>
    /// <param name="EventId">The event these rows belong to.</param>
    /// <param name="Detection">The event's detection summary, or null when the event has no detection row.</param>
    /// <param name="Occurrences">Detection-type occurrences, oldest first.</param>
    /// <param name="Zones">Zones the detection fired in.</param>
    /// <param name="People">People recognised in the event.</param>
    /// <param name="SecurityAlerts">Security alerts raised for the event.</param>
    public sealed record EventDetailDto(
        Guid EventId,
        EventDetailDto.EventDetectionDto? Detection,
        IReadOnlyList<EventDetailDto.EventOccurrenceDto> Occurrences,
        IReadOnlyList<EventDetailDto.EventZoneDto> Zones,
        IReadOnlyList<EventDetailDto.EventPersonDto> People,
        IReadOnlyList<EventDetailDto.EventSecurityAlertDto> SecurityAlerts)
    {
        /// <summary>Detection summary for an event.</summary>
        public sealed record EventDetectionDto(
            bool? PersonDetected,
            bool? StreamBroken,
            string? DetectionType,
            string? FullDescription,
            string? ShortDescription,
            decimal? Similarity,
            decimal? Anomaly,
            decimal? Confidence,
            string? ModelVersion);

        /// <summary>One detection-type occurrence with the time it was detected.</summary>
        public sealed record EventOccurrenceDto(string DetectionType, DateTime DetectedAtUtc);

        /// <summary>A zone the detection fired in.</summary>
        public sealed record EventZoneDto(string ZoneId, string? ZoneName, decimal? Confidence);

        /// <summary>A person recognised in the event.</summary>
        public sealed record EventPersonDto(string ProfileId, string? ProfileName, decimal? Confidence, string? ThumbnailUrl);

        /// <summary>A security alert raised for the event.</summary>
        public sealed record EventSecurityAlertDto(string? Severity, string? AlertText);
    }

    /// <summary>Extension methods for mapping event detail rows to <see cref="EventDetailDto"/>.</summary>
    public static class EventDetailDtoMapping
    {
        /// <summary>
        /// Converts an <see cref="EventDetail"/> to its DTO. Child lists keep the order the repository returned them in,
        /// so occurrences stay oldest first.
        /// </summary>
        public static EventDetailDto ToDto(this EventDetail detail)
        {
            return new EventDetailDto(
                EventId: detail.EventId,
                Detection: detail.Detection is null ? null : new EventDetailDto.EventDetectionDto(
                    PersonDetected: detail.Detection.PersonDetected,
                    StreamBroken: detail.Detection.StreamBroken,
                    DetectionType: detail.Detection.DetectionType,
                    FullDescription: detail.Detection.FullDescription,
                    ShortDescription: detail.Detection.ShortDescription,
                    Similarity: detail.Detection.Similarity,
                    Anomaly: detail.Detection.Anomaly,
                    Confidence: detail.Detection.Confidence,
                    ModelVersion: detail.Detection.ModelVersion),
                Occurrences: [.. detail.Occurrences.Select(o => new EventDetailDto.EventOccurrenceDto(o.DetectionType, o.DetectedAtUtc))],
                Zones: [.. detail.Zones.Select(z => new EventDetailDto.EventZoneDto(z.ZoneId, z.ZoneName, z.Confidence))],
                People: [.. detail.People.Select(p => new EventDetailDto.EventPersonDto(p.ProfileId, p.ProfileName, p.Confidence, p.ThumbnailUrl))],
                SecurityAlerts: [.. detail.SecurityAlerts.Select(a => new EventDetailDto.EventSecurityAlertDto(a.Severity, a.AlertText))]);
        }

        /// <summary>
        /// Converts an <see cref="EventDetailDto"/> back to an <see cref="EventDetail"/> for client-side reads. The DTO does not
        /// carry the row keys, so every entity id (and the detection id on child rows) is <see cref="Guid.Empty"/>; the
        /// Detail tab reads values only and never writes these rows back.
        /// </summary>
        public static EventDetail ToDomain(this EventDetailDto dto)
        {
            Guid eventId = dto.EventId;
            return new EventDetail(
                eventId,
                dto.Detection is null ? null : new EventDetection
                {
                    Id = Guid.Empty,
                    EventId = eventId,
                    PersonDetected = dto.Detection.PersonDetected,
                    StreamBroken = dto.Detection.StreamBroken,
                    DetectionType = dto.Detection.DetectionType,
                    FullDescription = dto.Detection.FullDescription,
                    ShortDescription = dto.Detection.ShortDescription,
                    Similarity = dto.Detection.Similarity,
                    Anomaly = dto.Detection.Anomaly,
                    Confidence = dto.Detection.Confidence,
                    ModelVersion = dto.Detection.ModelVersion,
                },
                [.. dto.Occurrences.Select(o => new EventDetectionTypeOccurrence
                {
                    Id = Guid.Empty,
                    EventDetectionId = Guid.Empty,
                    DetectionType = o.DetectionType,
                    DetectedAtUtc = o.DetectedAtUtc,
                })],
                [.. dto.Zones.Select(z => new EventDetectionZone
                {
                    Id = Guid.Empty,
                    EventDetectionId = Guid.Empty,
                    ZoneId = z.ZoneId,
                    ZoneName = z.ZoneName,
                    Confidence = z.Confidence,
                })],
                [.. dto.People.Select(p => new EventDetectedPerson
                {
                    Id = Guid.Empty,
                    EventId = eventId,
                    ProfileId = p.ProfileId,
                    ProfileName = p.ProfileName,
                    Confidence = p.Confidence,
                    ThumbnailUrl = p.ThumbnailUrl,
                })],
                [.. dto.SecurityAlerts.Select(a => new EventSecurityAlert
                {
                    Id = Guid.Empty,
                    EventId = eventId,
                    Severity = a.Severity,
                    AlertText = a.AlertText,
                })]);
        }
    }
}
