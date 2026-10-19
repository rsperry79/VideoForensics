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

        /// <summary>Stickup cam enable_vod setting.</summary>
        public bool? EnableVod { get; set; }

        public int? ExposureControl { get; set; }

        public string? MotionSnoozePresetProfile { get; set; }

        public string? LiveViewPresetProfile { get; set; }

        public int? PirSensitivity1 { get; set; }

        public bool? VodSuspended { get; set; }

        public int? DoorbellVolume { get; set; }

        public string? VodStatus { get; set; }

        public bool? AdvancedMotionDetectionEnabled { get; set; }

        public bool? AdvancedMotionDetectionHumanOnlyMode { get; set; }

        public bool? EnableAudioRecording { get; set; }

        public bool? EnableWhiteLeds { get; set; }

        /// <summary>Chime volume setting.</summary>
        public int? ChimeVolume { get; set; }

        public string? DingAudioUserId { get; set; }

        public string? DingAudioId { get; set; }

        public string? MotionAudioUserId { get; set; }

        public string? MotionAudioId { get; set; }

        /// <summary>Seconds remaining in a chime's do-not-disturb window; null when Ring reports no window.</summary>
        public int? DoNotDisturbSecondsLeft { get; set; }

        /// <summary>Stickup cam light brightness.</summary>
        public int? LightBrightness { get; set; }

        /// <summary>Ring owner id, stored as text.</summary>
        public string? OwnerId { get; set; }

        /// <summary>Owner name and email are personal data; they are stored only because the device record requires them.</summary>
        public string? OwnerFirstName { get; set; }

        public string? OwnerLastName { get; set; }

        public string? OwnerEmail { get; set; }

        /// <summary>Raw Ring device type from a doorbot, e.g. "doorbot".</summary>
        public string? RingType { get; set; }

        /// <summary>Ring's <c>ring_id</c> on a stickup cam.</summary>
        public string? RingRingId { get; set; }

        /// <summary>Stickup cam battery_life_2, stored as text like BatteryLife.</summary>
        public string? BatteryLife2 { get; set; }

        public decimal? BatteryVoltage { get; set; }

        public decimal? BatteryVoltage2 { get; set; }

        /// <summary>Stickup cam led_status as text: the string value, or the raw JSON when Ring sends an object or number.</summary>
        public string? LedStatus { get; set; }

        /// <summary>Seconds remaining on a stickup cam siren_status.</summary>
        public int? SirenSecondsRemaining { get; set; }

        public int? NightMode { get; set; }

        /// <summary>Raw JSON of motion_snooze. Ring sends an object on doorbots and a value of undetermined shape on stickup cams, so it is kept as received.</summary>
        public string? MotionSnoozeJson { get; set; }

        /// <summary>Raw JSON of settings keys not modelled by a named column (doorbot and stickup cam settings).</summary>
        public string? SettingsMetadataJson { get; set; }

        /// <summary>Raw JSON of light_settings keys other than brightness.</summary>
        public string? LightSettingsMetadataJson { get; set; }

        /// <summary>pir_settings.sensitivity_1..3 and zone fields. Separate from PirSensitivity1, which is the top-level pir_sensitivity_1.</summary>
        public int? PirSettingsSensitivity1 { get; set; }

        public int? PirSettingsSensitivity2 { get; set; }

        public int? PirSettingsSensitivity3 { get; set; }

        public int? PirSettingsZoneEnable { get; set; }

        public int? PirSettingsZoneMask { get; set; }

        /// <summary>Doorbot settings.night_mode_on.</summary>
        public bool? NightModeOn { get; set; }

        /// <summary>Doorbot settings.motion_detection_enabled.</summary>
        public bool? MotionDetectionEnabled { get; set; }

        /// <summary>Doorbot settings.chime_settings.type.</summary>
        public int? SettingsChimeType { get; set; }

        /// <summary>Doorbot settings.chime_settings.enable.</summary>
        public bool? SettingsChimeEnabled { get; set; }

        /// <summary>Doorbot settings.chime_settings.duration.</summary>
        public int? SettingsChimeDuration { get; set; }

        /// <summary>
        /// Local kill switch for server-side snapshot capture (SnapshotCaptureService). Not a Ring setting: Ring syncs update an
        /// existing row in place and leave it alone. A device moved to another location gets a new row, and EnsureDeviceAsync copies this flag onto it.
        /// </summary>
        public bool SnapshotCaptureEnabled { get; set; } = true;
    }
}
