namespace VideoForensics.Data.Common.Entities
{
    /// <summary>A device (camera, doorbell, etc.) linked to a location.</summary>
    public class Device
    {
        public Guid Id { get; set; }
        public Guid LocationId { get; set; }
        public required string ProviderDeviceId { get; set; }
        public required string Name { get; set; }
        public required string Type { get; set; }
        /// <summary>Last known online state; null when the provider gave no connection or subscription state.</summary>
        public bool? IsOnline { get; set; }
        public string? MetadataJson { get; set; }
        public DateTime? LastSuccessfulPullAtUtc { get; set; }
        public DateTime? LastPullAttemptAtUtc { get; set; }
        public string? TimeZoneId { get; set; }
        public DateTime? LastSyncedUtc { get; set; }
        public SyncStatus SyncStatus { get; set; } = SyncStatus.Pending;
        public string? ApiResponseHash { get; set; }
        public Guid? DeviceHealthId { get; set; }
        public Guid? DeviceFeaturesId { get; set; }
        public Guid? DeviceLocationId { get; set; }

        /// <summary>True when the device's clean bitrate history shows a steady drift; null if not yet checked.</summary>
        public bool? BaselineDriftDetected { get; set; }

        /// <summary>Relative change in the device's bitrate median across its history, earliest to latest window.</summary>
        public double? BaselineDriftChangeFraction { get; set; }

        /// <summary>When the drift check last ran.</summary>
        public DateTime? BaselineDriftCheckedAtUtc { get; set; }

        /// <summary>Ring's hex device_id (distinct from ProviderDeviceId, which is the numeric id).</summary>
        public string? RingDeviceId { get; set; }

        /// <summary>Raw Ring device kind, e.g. doorbell_v4.</summary>
        public string? RingKind { get; set; }

        public string? FirmwareVersion { get; set; }

        public double? Latitude { get; set; }

        public double? Longitude { get; set; }

        /// <summary>The device's own address field. Not Locations.Address.</summary>
        public string? Address { get; set; }

        public bool? Subscribed { get; set; }

        public bool? SubscribedMotions { get; set; }

        /// <summary>Ring's battery_life kept as the raw value (Ring sends a number on some kinds, so it is stored as text).</summary>
        public string? BatteryLife { get; set; }

        public bool? ExternalConnection { get; set; }

        public bool? Owned { get; set; }

        public bool? Stolen { get; set; }
    }
}
