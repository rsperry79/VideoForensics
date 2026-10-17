namespace VideoForensics.Data.Common.Entities
{
    /// <summary>One video search result (clients_api/video_search/history). Nested objects are stored in <see cref="RingEventDetails"/>.</summary>
    public class RingVideoSearchEvent
    {
        public Guid Id { get; set; }

        public Guid RingAccountId { get; set; }

        /// <summary>Ring "ding_id" of the event.</summary>
        public string? ProviderEventId { get; set; }

        /// <summary>Ring "id" of the search item, when Ring returns one.</summary>
        public long? RingItemId { get; set; }

        /// <summary>Epoch milliseconds from "created_at".</summary>
        public long? CreatedAtMs { get; set; }

        /// <summary>Raw "updated_at" value (Ring returns it in microseconds).</summary>
        public long? UpdatedAtMs { get; set; }

        public string? Kind { get; set; }

        public string? State { get; set; }

        public int? DurationSeconds { get; set; }

        public bool? Favorite { get; set; }

        /// <summary>Temporary pre-signed download URL; expires quickly.</summary>
        public string? HqUrl { get; set; }

        public string? LqUrl { get; set; }

        public string? ThumbnailUrl { get; set; }

        public string? UntranscodedUrl { get; set; }

        public bool? IsE2ee { get; set; }

        public string? ManifestId { get; set; }

        public double? PrerollDuration { get; set; }

        public bool? HadSubscription { get; set; }

        public string? RadarDataUrl { get; set; }

        public string? OwnerId { get; set; }

        public string? SourceId { get; set; }

        public string? Riid { get; set; }

        /// <summary>Ring returns battery percentage as a string.</summary>
        public string? BatteryPercentage { get; set; }

        public string? BatteryStatus { get; set; }
    }

    /// <summary>The location-events paging cursor for one location.</summary>
    public class RingLocationEventPage
    {
        public Guid Id { get; set; }

        public string ProviderLocationId { get; set; } = string.Empty;

        public string? PaginationKey { get; set; }
    }

    /// <summary>One event from clients_api/locations/{id}/events. Nested objects are stored in <see cref="RingEventDetails"/>.</summary>
    public class RingLocationEvent
    {
        public Guid Id { get; set; }

        public string ProviderLocationId { get; set; } = string.Empty;

        /// <summary>Ring "event_id" of the event.</summary>
        public string? ProviderEventId { get; set; }

        public string? SourceId { get; set; }

        public string? EventType { get; set; }

        public string? State { get; set; }

        public bool? Favorite { get; set; }

        public bool? Recorded { get; set; }

        public string? RecordingStatus { get; set; }

        public bool? IsE2ee { get; set; }

        /// <summary>Raw "created_at" string as Ring returns it.</summary>
        public string? CreatedAt { get; set; }

        public bool? HadSubscription { get; set; }

        public string? OwnerId { get; set; }

        public string? Riid { get; set; }

        public long? DoorbotId { get; set; }

        public long? DingId { get; set; }

        public string? DingIdString { get; set; }

        public string? Kind { get; set; }

        public string? BatteryPercentage { get; set; }

        public string? BatteryStatus { get; set; }
    }

    /// <summary>One doorbot history event (doorbots/history). Nested objects are stored in <see cref="RingEventDetails"/>.</summary>
    public class RingDoorbotHistoryEvent
    {
        public Guid Id { get; set; }

        /// <summary>Ring numeric "id" of the history event, stored as text. Null when Ring omits it.</summary>
        public string? ProviderEventId { get; set; }

        /// <summary>Raw "created_at" string as Ring returns it.</summary>
        public string? CreatedAt { get; set; }

        public bool? Answered { get; set; }

        public string? Kind { get; set; }

        public bool? Favorite { get; set; }

        public string? SnapshotUrl { get; set; }

        public int? Duration { get; set; }

        public bool? IsE2ee { get; set; }

        public bool? HadSubscription { get; set; }

        public string? OwnerId { get; set; }

        public string? Riid { get; set; }
    }
}
