using VideoForensics.Data.Common.Contracts;

namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>Offline motion upload and sampling settings for one device.</summary>
    public sealed record DeviceOfflineMotionDto(bool? Subscribed, bool? Enabled, int? MaxUploadKb, int? ResolutionP, int? FrequencyAfterSecs, int? PeriodAfterSecs);

    /// <summary>One CV detection type with its mode and output flags.</summary>
    public sealed record DeviceDetectionTypeDto(string DetectionType, bool? Enabled, string? Mode, bool? Record, bool? Notification);

    /// <summary>One numeric CV threshold for a detection type.</summary>
    public sealed record DeviceDetectionThresholdDto(string DetectionType, double? Threshold);

    /// <summary>The typed settings stored for a device, drillable by group.</summary>
    public sealed record DeviceTypedSettingsDto(
        Guid DeviceId,
        DeviceOfflineMotionDto? OfflineMotion,
        IReadOnlyList<DeviceDetectionTypeDto> DetectionTypes,
        IReadOnlyList<DeviceDetectionThresholdDto> Thresholds);

    /// <summary>Maps the typed settings read model to its DTO.</summary>
    public static class DeviceTypedSettingsDtoMapping
    {
        /// <summary>Converts the typed rows for <paramref name="deviceId"/> to a DTO.</summary>
        public static DeviceTypedSettingsDto ToDto(this DeviceTypedSettings settings, Guid deviceId)
        {
            DeviceOfflineMotionDto? offline = settings.OfflineMotion is { } o
                ? new DeviceOfflineMotionDto(o.Subscribed, o.Enabled, o.MaxUploadKb, o.ResolutionP, o.FrequencyAfterSecs, o.PeriodAfterSecs)
                : null;
            return new DeviceTypedSettingsDto(
                deviceId,
                offline,
                settings.DetectionTypes.Select(t => new DeviceDetectionTypeDto(t.DetectionType, t.Enabled, t.Mode, t.Record, t.Notification)).ToList(),
                settings.Thresholds.Select(t => new DeviceDetectionThresholdDto(t.DetectionType, t.Threshold)).ToList());
        }
    }
}
