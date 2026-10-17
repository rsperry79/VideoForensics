namespace VideoForensics.Data.Common.Entities
{
    /// <summary>The evm location history paging cursor for one location.</summary>
    public class RingLocationHistoryPage
    {
        public Guid Id { get; set; }

        public string ProviderLocationId { get; set; } = string.Empty;

        public string? PaginationKey { get; set; }
    }

    /// <summary>One entry of the evm location history feed. Nested objects are stored in <see cref="RingEventDetails"/>.</summary>
    public class RingLocationHistoryEvent
    {
        public Guid Id { get; set; }

        public string ProviderLocationId { get; set; } = string.Empty;

        /// <summary>Ring "event_id" of the entry.</summary>
        public string? ProviderEventId { get; set; }

        public string? Schema { get; set; }

        public string? SourceId { get; set; }

        public string? StartTime { get; set; }

        public string? EndTime { get; set; }

        public string? EventType { get; set; }

        public string? SourceType { get; set; }

        public long? DurationMs { get; set; }

        public long? SessionDurationMs { get; set; }

        public string? State { get; set; }

        public bool? HadSubscription { get; set; }

        public bool? IsFavorite { get; set; }

        public string? RecordingStatus { get; set; }

        public string? Origin { get; set; }

        public string? OriginDisplayName { get; set; }

        public string? ErrorMessage { get; set; }

        public string? UpdatedAt { get; set; }

        public string? OwnerId { get; set; }

        public bool? DeleteInProgress { get; set; }

        public bool? LowConfidence { get; set; }

        public double? Similarity { get; set; }

        public string? Riid { get; set; }

        public bool? UserSpecifiedRecordingDisabled { get; set; }

        public bool? IsWatched { get; set; }

        public bool? IsPropertyViewEnabled { get; set; }

        public bool? IsActive { get; set; }

        public bool? UserActionDisabled { get; set; }

        public string? Mission { get; set; }

        public List<RingLocationHistoryVisualization> Visualizations { get; set; } = [];
    }

    /// <summary>One visualization of a location history entry (cloud or local media).</summary>
    public class RingLocationHistoryVisualization
    {
        public Guid Id { get; set; }

        public Guid RingLocationHistoryEventId { get; set; }

        /// <summary>"cloud" or "local", which of the two visualization objects this row came from.</summary>
        public string Kind { get; set; } = string.Empty;

        public string? Schema { get; set; }

        public List<RingLocationHistoryMedia> Media { get; set; } = [];
    }

    /// <summary>One media item of a visualization.</summary>
    public class RingLocationHistoryMedia
    {
        public Guid Id { get; set; }

        public Guid RingLocationHistoryVisualizationId { get; set; }

        public string? Schema { get; set; }

        public string? Url { get; set; }

        public string? StartTime { get; set; }

        public string? EndTime { get; set; }

        public bool? IsE2ee { get; set; }

        public string? ManifestId { get; set; }

        public string? FileType { get; set; }

        public string? FileFamily { get; set; }

        public long? PrerollDurationMs { get; set; }

        public long? PlaybackDuration { get; set; }

        public string? Source { get; set; }

        public RingLocationHistoryMediaMetadata? Metadata { get; set; }
    }

    /// <summary>The custom_metadata object of a media item.</summary>
    public class RingLocationHistoryMediaMetadata
    {
        public Guid Id { get; set; }

        public Guid RingLocationHistoryMediaId { get; set; }

        public bool? Recording24x7Enabled { get; set; }

        public string? DingId { get; set; }

        public string? AudioEncoding { get; set; }

        public string? VideoEncoding { get; set; }

        public bool? TbaTranscodingEnabled { get; set; }

        public List<RingLocationHistoryMediaTrack> Tracks { get; set; } = [];
    }

    /// <summary>One entry of custom_metadata.tracks.</summary>
    public class RingLocationHistoryMediaTrack
    {
        public Guid Id { get; set; }

        public Guid RingLocationHistoryMediaMetadataId { get; set; }

        public int Position { get; set; }

        public string? TrackId { get; set; }

        public string? Codec { get; set; }

        public string? Type { get; set; }
    }

    /// <summary>One message of the rs/history app feed. Each message is one row; its bodies are child rows.</summary>
    public class RingAppLocationHistoryMessage
    {
        public Guid Id { get; set; }

        public int Seq { get; set; }

        public string? Msg { get; set; }

        public string? DataType { get; set; }

        public string? Src { get; set; }

        /// <summary>context.eventId, unique per message.</summary>
        public string? EventId { get; set; }

        public long? EventPublishedTsMs { get; set; }

        public long? RequestSeq { get; set; }

        public string? AffectedEntityId { get; set; }

        public string? DeviceFamily { get; set; }

        public string? InitiatingEntityType { get; set; }

        public string? AffectedEntityType { get; set; }

        public string? ForwarderTopic { get; set; }

        public string? AccountId { get; set; }

        public long? RequestSessionId { get; set; }

        public int? EventLevel { get; set; }

        public string? InitiatingEntityId { get; set; }

        public long? EventOccurredTsMs { get; set; }

        public string? AffectedEntityName { get; set; }

        public string? ProgramId { get; set; }

        public List<RingAppLocationHistoryBody> Bodies { get; set; } = [];
    }

    /// <summary>One entry of a message body (one device).</summary>
    public class RingAppLocationHistoryBody
    {
        public Guid Id { get; set; }

        public Guid RingAppLocationHistoryMessageId { get; set; }

        public int Position { get; set; }

        public string? DeviceType { get; set; }

        public string? CatalogId { get; set; }

        public string? Zid { get; set; }

        public long? LastUpdate { get; set; }

        public string? AdapterType { get; set; }

        public string? BatteryStatus { get; set; }

        public int? SubCategoryId { get; set; }

        public string? DeviceName { get; set; }

        public int? CategoryId { get; set; }

        public string? RoomName { get; set; }

        public RingAppLocationHistoryDeviceState? DeviceState { get; set; }

        public List<RingAppLocationHistoryImpulse> Impulses { get; set; } = [];
    }

    /// <summary>The device state of a body (context.v1.device.v1).</summary>
    public class RingAppLocationHistoryDeviceState
    {
        public Guid Id { get; set; }

        public Guid RingAppLocationHistoryBodyId { get; set; }

        public string? Mode { get; set; }

        public string? SirenState { get; set; }

        public List<RingAppLocationHistoryDeviceMode> Modes { get; set; } = [];
    }

    /// <summary>The settings of one mode ("all", "some" or "none") of a device state.</summary>
    public class RingAppLocationHistoryDeviceMode
    {
        public Guid Id { get; set; }

        public Guid RingAppLocationHistoryDeviceStateId { get; set; }

        public string ModeName { get; set; } = string.Empty;

        public int? TransitionDelay { get; set; }

        public bool? Editable { get; set; }

        public bool? Removable { get; set; }

        public bool? ChirpsEnabled { get; set; }

        public int? EntryDelay { get; set; }
    }

    /// <summary>One impulse of a body (impulse.v1).</summary>
    public class RingAppLocationHistoryImpulse
    {
        public Guid Id { get; set; }

        public Guid RingAppLocationHistoryBodyId { get; set; }

        public int Position { get; set; }

        public string? ImpulseType { get; set; }

        public string? CommandType { get; set; }

        /// <summary>data.data.mode of the impulse command.</summary>
        public string? DataMode { get; set; }
    }
}
