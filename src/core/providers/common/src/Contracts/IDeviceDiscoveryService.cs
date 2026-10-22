namespace VideoForensics.Core.Providers.Common.Contracts
{
    /// <summary>Platform-agnostic device discovery interface</summary>
    public interface IDeviceDiscoveryService
    {
        /// <summary>Gets all locations/sites for the authenticated user</summary>
        Task<IReadOnlyList<Location>> GetLocationsAsync(CancellationToken cancellationToken = default);

        /// <summary>Gets all devices at a specific location</summary>
        Task<IReadOnlyList<Device>> GetDevicesAsync(string locationId, CancellationToken cancellationToken = default);

        /// <summary>Gets a specific device details</summary>
        Task<Device?> GetDeviceAsync(string deviceId, CancellationToken cancellationToken = default);

        /// <summary>Gets all locations/sites for a specific provider account (account-aware overload)</summary>
        Task<IReadOnlyList<Location>> GetLocationsAsync(Guid providerAccountId, CancellationToken cancellationToken = default);

        /// <summary>Gets all devices at a specific location for a specific provider account (account-aware overload)</summary>
        Task<IReadOnlyList<Device>> GetDevicesAsync(Guid providerAccountId, string locationId, CancellationToken cancellationToken = default);

        /// <summary>Gets a specific device details for a specific provider account (account-aware overload)</summary>
        Task<Device?> GetDeviceAsync(Guid providerAccountId, string deviceId, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// Optional capability for discovery services that capture location metadata. Callers must
    /// invoke it only after the Location row exists in the database, because the metadata row is
    /// keyed by the stored Location id; calling it earlier silently skips capture.
    /// </summary>
    public interface ILocationMetadataPersister
    {
        /// <summary>Persists metadata for the given discovered locations (subject to provider-side throttling).</summary>
        Task PersistLocationMetadataAsync(IReadOnlyList<Location> locations, CancellationToken cancellationToken = default);
    }

    /// <summary>Represents a location/site (home, office, etc.)</summary>
    public record Location(
        string Id,
        string Name,
        string? Address = null,
        Dictionary<string, string>? Metadata = null
    );

    /// <summary>Represents a security device (camera, doorbell, etc.)</summary>
    public record Device(
        string Id,
        string Name,
        string Type, // "camera", "doorbell", "base_station", etc.
        string LocationId,
        bool? IsOnline,
        Dictionary<string, string>? Metadata = null,
        DeviceDetailFields? Details = null
    );

    /// <summary>
    /// Provider-supplied scalar device fields stored on the device row. Every property is optional:
    /// null means the provider did not supply that value, and it is stored as null (never inferred).
    /// </summary>
    public sealed record DeviceDetailFields(
        string? RingDeviceId = null,
        string? RingKind = null,
        string? FirmwareVersion = null,
        double? Latitude = null,
        double? Longitude = null,
        string? Address = null,
        bool? Subscribed = null,
        bool? SubscribedMotions = null,
        string? BatteryLife = null,
        bool? ExternalConnection = null,
        bool? Owned = null,
        bool? Stolen = null,
        bool? EnableVod = null,
        int? ExposureControl = null,
        string? MotionSnoozePresetProfile = null,
        string? LiveViewPresetProfile = null,
        int? PirSensitivity1 = null,
        bool? VodSuspended = null,
        int? DoorbellVolume = null,
        string? VodStatus = null,
        bool? AdvancedMotionDetectionEnabled = null,
        bool? AdvancedMotionDetectionHumanOnlyMode = null,
        bool? EnableAudioRecording = null,
        bool? EnableWhiteLeds = null,
        int? ChimeVolume = null,
        string? DingAudioUserId = null,
        string? DingAudioId = null,
        string? MotionAudioUserId = null,
        string? MotionAudioId = null,
        int? DoNotDisturbSecondsLeft = null,
        int? LightBrightness = null,
        string? OwnerId = null,
        string? OwnerFirstName = null,
        string? OwnerLastName = null,
        string? OwnerEmail = null,
        IReadOnlyList<string>? MotionSnoozePresets = null,
        IReadOnlyList<string>? LiveViewPresets = null,
        IReadOnlyList<AdvancedMotionZoneDetail>? AdvancedMotionZones = null,
        string? RingType = null,
        string? RingRingId = null,
        string? BatteryLife2 = null,
        decimal? BatteryVoltage = null,
        decimal? BatteryVoltage2 = null,
        string? LedStatus = null,
        int? SirenSecondsRemaining = null,
        int? NightMode = null,
        string? MotionSnoozeJson = null,
        string? SettingsMetadataJson = null,
        string? LightSettingsMetadataJson = null,
        int? PirSettingsSensitivity1 = null,
        int? PirSettingsSensitivity2 = null,
        int? PirSettingsSensitivity3 = null,
        int? PirSettingsZoneEnable = null,
        int? PirSettingsZoneMask = null,
        bool? NightModeOn = null,
        bool? MotionDetectionEnabled = null,
        int? SettingsChimeType = null,
        bool? SettingsChimeEnabled = null,
        int? SettingsChimeDuration = null,
        IReadOnlyList<DeviceZoneDetail>? Zones = null,
        IReadOnlyList<AdvancedObjectSettingDetail>? AdvancedObjectSettings = null,
        OfflineMotionDetail? OfflineMotion = null,
        IReadOnlyList<DetectionTypeDetail>? DetectionTypes = null,
        IReadOnlyList<DetectionThresholdDetail>? DetectionThresholds = null
    );

    /// <summary>
    /// One motion or package zone from zone_settings. Source is "motion" or "package". Detection types keep Ring order;
    /// vertices use the 1-based list position as Position.
    /// </summary>
    public sealed record DeviceZoneDetail(
        string Source,
        string? ZoneId,
        string? Name,
        int? State,
        string? Style,
        IReadOnlyList<string> DetectionTypes,
        IReadOnlyList<AdvancedMotionZoneVertexDetail> Vertices);

    /// <summary>One advanced object setting (e.g. human_detection_confidence) with its day and night values.</summary>
    public sealed record AdvancedObjectSettingDetail(string SettingKey, double? Day, double? Night);

    /// <summary>Ring offline_motion_event_settings: how motion recorded while offline is uploaded and sampled.</summary>
    public sealed record OfflineMotionDetail(bool? Subscribed, bool? Enabled, int? MaxUploadKb, int? ResolutionP, int? FrequencyAfterSecs, int? PeriodAfterSecs);

    /// <summary>One cv_settings.detection_types entry (e.g. "human") with its mode and output flags.</summary>
    public sealed record DetectionTypeDetail(string DetectionType, bool? Enabled, string? Mode, bool? Record, bool? Notification);

    /// <summary>One numeric cv_settings.threshold entry for a detection type.</summary>
    public sealed record DetectionThresholdDetail(string DetectionType, double Threshold);

    /// <summary>An advanced motion zone (zone1..zone3) as Ring reports it. Vertices are listed in Ring order.</summary>
    public sealed record AdvancedMotionZoneDetail(string ZoneKey, string? Name, int? State, IReadOnlyList<AdvancedMotionZoneVertexDetail> Vertices);

    /// <summary>One zone vertex. Position is Ring's 1-based vertex number (vertex1..vertex8).</summary>
    public sealed record AdvancedMotionZoneVertexDetail(int Position, double? X, double? Y);
}
