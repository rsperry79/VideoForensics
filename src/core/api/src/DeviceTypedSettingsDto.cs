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
}
