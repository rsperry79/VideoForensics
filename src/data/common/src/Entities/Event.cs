namespace VideoForensics.Data.Common.Entities
{
    /// <summary>Download status for an event.</summary>
    public enum EventDownloadStatus
    {
        /// <summary>No download attempt has been made.</summary>
        NotAttempted = 0,

        /// <summary>Download was attempted but failed.</summary>
        DownloadFailed = 1,

        /// <summary>Event was successfully downloaded.</summary>
        Downloaded = 2
    }

    /// <summary>An event detected by a device, independent of whether it was downloaded.</summary>
    public class Event
    {
        public Guid Id { get; set; }
        public Guid DeviceId { get; set; }
        public required string ProviderEventId { get; set; }
        public required string EventType { get; set; }
        public DateTime OccurredAtUtc { get; set; }
        public string? SnapshotUrl { get; set; }
        /// <summary>
        /// Only what has no structured home: keys the provider mapper does not store, or an app marker. The raw provider
        /// payload is NOT stored here (it is in the typed provider tables), so <see cref="ApiSourceHash"/> and
        /// <see cref="EventIntegrityHash"/> can no longer be re-verified from the database alone. Null when nothing is left.
        /// Rows written before this change may still hold the whole raw payload.
        /// </summary>
        public string? MetadataJson { get; set; }
        public DateTime DiscoveredAtUtc { get; set; }
        public DateTime? DownloadedAtUtc { get; set; }
        /// <summary>
        /// SHA-256 (upper-case hex) of the full raw provider payload as serialized at sync time. The payload itself is no
        /// longer stored (see <see cref="MetadataJson"/>), so this hash cannot be re-verified from the database alone.
        /// </summary>
        public string? ApiSourceHash { get; set; }
        public string? EventIntegrityHash { get; set; }
        public Guid? EventDetectionId { get; set; }
        public string? RecordingStatus { get; set; }
        public EventDownloadStatus DownloadStatus { get; set; } = EventDownloadStatus.NotAttempted;
        public DateTime? DownloadFailedAtUtc { get; set; }
    }
}
