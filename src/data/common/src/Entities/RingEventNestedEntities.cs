namespace VideoForensics.Data.Common.Entities
{
    /// <summary>The device a Ring event belongs to, as embedded in the event (id, description and type).</summary>
    public class RingEventDoorbot
    {
        public Guid Id { get; set; }

        public string Source { get; set; } = string.Empty;

        public Guid SourceRowId { get; set; }

        public long? DoorbotId { get; set; }

        public string? Description { get; set; }

        public string? Type { get; set; }
    }

    /// <summary>Boolean and numeric event flags (the "properties" object).</summary>
    public class RingEventProperties
    {
        public Guid Id { get; set; }

        public string Source { get; set; } = string.Empty;

        public Guid SourceRowId { get; set; }

        public bool? IsAlexa { get; set; }

        public bool? IsSidewalk { get; set; }

        public bool? IsAutoreply { get; set; }

        public bool? StarkReviewed { get; set; }

        public bool? PackagePickupDetected { get; set; }

        public double? DetectionConfidence { get; set; }
    }

    /// <summary>Where the device was placed when the event happened (the "device_placement" object).</summary>
    public class RingEventDevicePlacement
    {
        public Guid Id { get; set; }

        public string Source { get; set; } = string.Empty;

        public Guid SourceRowId { get; set; }

        public string? Room { get; set; }

        public string? Area { get; set; }

        public string? ZoneId { get; set; }
    }

    /// <summary>Geolocation reported with an event (the "geolocation" object).</summary>
    public class RingEventGeolocation
    {
        public Guid Id { get; set; }

        public string Source { get; set; } = string.Empty;

        public Guid SourceRowId { get; set; }

        public double? Latitude { get; set; }

        public double? Longitude { get; set; }

        public double? Accuracy { get; set; }

        public long? TimestampMs { get; set; }

        /// <summary>The "source" field of the geolocation object (not the event feed).</summary>
        public string? LocationSource { get; set; }
    }

    /// <summary>Subscription receipt attached to an event capture (the "subscription_receipt" object).</summary>
    public class RingEventSubscriptionReceipt
    {
        public Guid Id { get; set; }

        public string Source { get; set; } = string.Empty;

        public Guid SourceRowId { get; set; }

        public string? SubscriptionId { get; set; }

        public string? SubscriptionType { get; set; }

        public long? CapturedAt { get; set; }

        public long? ExpiresAt { get; set; }
    }

    /// <summary>Last known device location when the event happened (the "last_location" object).</summary>
    public class RingEventLastLocation
    {
        public Guid Id { get; set; }

        public string Source { get; set; } = string.Empty;

        public Guid SourceRowId { get; set; }

        public double? Latitude { get; set; }

        public double? Longitude { get; set; }

        public double? Accuracy { get; set; }

        public long? TimestampMs { get; set; }

        public string? Name { get; set; }
    }

    /// <summary>Alarm state associated with an event (the "siren" object).</summary>
    public class RingEventSiren
    {
        public Guid Id { get; set; }

        public string Source { get; set; } = string.Empty;

        public Guid SourceRowId { get; set; }

        public bool? Armed { get; set; }

        public long? TriggeredAt { get; set; }

        public string? TriggerType { get; set; }

        public string? AlarmState { get; set; }
    }

    /// <summary>Recording status of a doorbot history event (the "recording" object).</summary>
    public class RingEventRecording
    {
        public Guid Id { get; set; }

        public string Source { get; set; } = string.Empty;

        public Guid SourceRowId { get; set; }

        public string? Status { get; set; }
    }

    /// <summary>Computer vision properties of an event (the "cv_properties" object). Lists are child rows.</summary>
    public class RingEventCvProperties
    {
        public Guid Id { get; set; }

        public string Source { get; set; } = string.Empty;

        public Guid SourceRowId { get; set; }

        public bool? PersonDetected { get; set; }

        public bool? StreamBroken { get; set; }

        public string? DetectionType { get; set; }

        public string? FullDescription { get; set; }

        public string? ShortDescription { get; set; }

        public double? Similarity { get; set; }

        public double? Anomaly { get; set; }

        public List<RingEventCvDetectionType> DetectionTypes { get; set; } = [];

        /// <summary>At most one element: the security_alerts object, stored as a row rather than a JSON blob.</summary>
        public List<RingEventCvSecurityAlert> SecurityAlerts { get; set; } = [];

        public List<RingEventCvProfile> Profiles { get; set; } = [];

        public List<RingEventCvDetectionDetail> DetectionDetails { get; set; } = [];

        public List<RingEventCvTag> Tags { get; set; } = [];
    }

    /// <summary>One entry of cv_properties.detection_types.</summary>
    public class RingEventCvDetectionType
    {
        public Guid Id { get; set; }

        public Guid RingEventCvPropertiesId { get; set; }

        public int Position { get; set; }

        public string? DetectionType { get; set; }

        public List<RingEventCvVerifiedTimestamp> VerifiedTimestamps { get; set; } = [];
    }

    /// <summary>One verified_timestamps value of a detection type (epoch milliseconds).</summary>
    public class RingEventCvVerifiedTimestamp
    {
        public Guid Id { get; set; }

        public Guid RingEventCvDetectionTypeId { get; set; }

        public int Position { get; set; }

        public long TimestampMs { get; set; }
    }

    /// <summary>The security_alerts object of cv_properties.</summary>
    public class RingEventCvSecurityAlert
    {
        public Guid Id { get; set; }

        public Guid RingEventCvPropertiesId { get; set; }

        public string? Severity { get; set; }

        public List<RingEventCvSecurityAlertItem> Alerts { get; set; } = [];
    }

    /// <summary>One entry of security_alerts.alerts.</summary>
    public class RingEventCvSecurityAlertItem
    {
        public Guid Id { get; set; }

        public Guid RingEventCvSecurityAlertId { get; set; }

        public int Position { get; set; }

        public string AlertText { get; set; } = string.Empty;
    }

    /// <summary>One entry of cv_properties.profiles (a recognised person).</summary>
    public class RingEventCvProfile
    {
        public Guid Id { get; set; }

        public Guid RingEventCvPropertiesId { get; set; }

        public int Position { get; set; }

        public string? ProfileId { get; set; }

        public string? Name { get; set; }

        public double? Confidence { get; set; }

        public string? ThumbnailUrl { get; set; }
    }

    /// <summary>One entry of cv_properties.detection_details. The object form is one row with no timestamp.</summary>
    public class RingEventCvDetectionDetail
    {
        public Guid Id { get; set; }

        public Guid RingEventCvPropertiesId { get; set; }

        public int Position { get; set; }

        public long? TimestampMs { get; set; }

        public string? Type { get; set; }

        public double? Confidence { get; set; }

        public string? ModelVersion { get; set; }

        public List<RingEventCvDetectionZone> Zones { get; set; } = [];
    }

    /// <summary>One zone of a detection detail.</summary>
    public class RingEventCvDetectionZone
    {
        public Guid Id { get; set; }

        public Guid RingEventCvDetectionDetailId { get; set; }

        public int Position { get; set; }

        public string? ZoneId { get; set; }

        public string? Name { get; set; }

        public double? Confidence { get; set; }
    }

    /// <summary>One entry of cv_properties.tags.</summary>
    public class RingEventCvTag
    {
        public Guid Id { get; set; }

        public Guid RingEventCvPropertiesId { get; set; }

        public int Position { get; set; }

        public string Tag { get; set; } = string.Empty;
    }
}
