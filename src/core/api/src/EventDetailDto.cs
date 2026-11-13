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
}
