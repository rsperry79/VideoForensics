using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Contracts;
using System;
using System.Linq;

namespace VideoForensics.Core.Api.Contracts
{
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
