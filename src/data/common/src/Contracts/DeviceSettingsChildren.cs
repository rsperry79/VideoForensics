using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>
    /// Settings child rows for one device, replaced as a set on each write. Each list is the complete current
    /// value from Ring, so an empty list clears the rows already stored for that device. Zones and advanced object
    /// settings may be null, which leaves their stored rows untouched (the caller did not supply them).
    /// </summary>
    public sealed record DeviceSettingsChildren(
        IReadOnlyList<DeviceMotionSnoozePreset> MotionSnoozePresets,
        IReadOnlyList<DeviceLiveViewPreset> LiveViewPresets,
        IReadOnlyList<DeviceAdvancedMotionZone> AdvancedMotionZones,
        IReadOnlyList<DeviceZone>? Zones = null,
        IReadOnlyList<DeviceAdvancedObjectSetting>? AdvancedObjectSettings = null);
}
