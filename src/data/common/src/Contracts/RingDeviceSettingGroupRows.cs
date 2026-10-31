using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>
    /// The endpoint setting groups of one device: a 1:1 row per Ring group table (plus the misc table), and the motion snooze and cv threshold child rows.
    /// A null group leaves its stored row untouched; a non-null group writes its row, with null columns where Ring sent no key. Each child list is the
    /// complete current value from Ring, so an empty list clears the stored rows.
    /// </summary>
    public sealed record RingDeviceSettingGroupRows(
        RingDeviceAdvancedMotionSettings? AdvancedMotion,
        RingDeviceBackendSettings? Backend,
        RingDeviceClientDeviceSettings? ClientDevice,
        RingDeviceCvSettings? Cv,
        RingDeviceGeneralSettings? General,
        RingDeviceMiscSettings? Misc,
        RingDeviceMotionSettings? Motion,
        RingDeviceSnapshotSettings? Snapshot,
        RingDeviceStreamSettings? Stream,
        RingDeviceVideoSettings? Video,
        RingDeviceVodSettings? Vod,
        IReadOnlyList<RingDeviceMotionSnoozeProfile>? MotionSnoozeProfiles,
        IReadOnlyList<RingDeviceCvThresholdOverrideVersion>? CvThresholdVersions,
        IReadOnlyList<RingDeviceCvThresholdOverrideValue>? CvThresholdValues);
}
