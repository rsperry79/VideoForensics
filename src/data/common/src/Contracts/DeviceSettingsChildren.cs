using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>
    /// Settings child rows for one device, replaced as a set on each write. Each list is the complete current
    /// value from Ring, so an empty list clears the rows already stored for that device. Zones, advanced object settings, offline motion, detection types, thresholds, feature states, the reported health row and its batteries may be null, which leaves their stored rows untouched (the caller did not supply them).
    /// </summary>
    /// <remarks>LightSecondsRemaining is the reported light on-time; when above zero and the light was not already on, a light event is recorded. LightIsOn is the on/off state for providers that report no duration (true records an on event with no seconds on a transition from off). Null means the caller supplied none.</remarks>
    public sealed record DeviceSettingsChildren(
        IReadOnlyList<DeviceMotionSnoozePreset> MotionSnoozePresets,
        IReadOnlyList<DeviceLiveViewPreset> LiveViewPresets,
        IReadOnlyList<DeviceAdvancedMotionZone> AdvancedMotionZones,
        IReadOnlyList<DeviceZone>? Zones = null,
        IReadOnlyList<DeviceAdvancedObjectSetting>? AdvancedObjectSettings = null,
        IReadOnlyList<DeviceOfflineMotionSettings>? OfflineMotion = null,
        IReadOnlyList<DeviceDetectionType>? DetectionTypes = null,
        IReadOnlyList<DeviceDetectionThreshold>? DetectionThresholds = null,
        IReadOnlyList<DeviceFeatureState>? FeatureStates = null,
        DeviceReportedHealth? ReportedHealth = null,
        IReadOnlyList<DeviceReportedBattery>? ReportedBatteries = null,
        RingDeviceHealthExtras? RingHealthExtras = null,
        RingDeviceDetails? RingDetails = null,
        int? LightSecondsRemaining = null,
        bool? LightIsOn = null);
}
