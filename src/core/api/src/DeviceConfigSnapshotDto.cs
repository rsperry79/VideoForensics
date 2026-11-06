using System.Text.Json;

namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>
    /// Data transfer object for an append-only snapshot of device configuration settings.
    /// </summary>
    /// <param name="Id">Unique identifier for this configuration snapshot.</param>
    /// <param name="DeviceId">The device this configuration snapshot applies to.</param>
    /// <param name="MotionDetectionEnabled">Whether motion detection is enabled on the device, if known.</param>
    /// <param name="MotionSensitivity">The motion detection sensitivity setting, if applicable (vendor-specific format).</param>
    /// <param name="RecordingMode">The recording mode setting (e.g., "continuous", "motion-triggered"), if applicable.</param>
    /// <param name="CustomSettings">Additional vendor-specific configuration settings, one JSON value per setting key.</param>
    /// <param name="CapturedAtUtc">Timestamp when this configuration snapshot was captured, in UTC.</param>
    /// <param name="Source">Indicates whether this snapshot was fetched from the device/provider or represents an applied setting.</param>
    public record DeviceConfigSnapshotDto(
        Guid Id,
        Guid DeviceId,
        bool? MotionDetectionEnabled,
        string? MotionSensitivity,
        string? RecordingMode,
        Dictionary<string, JsonElement>? CustomSettings,
        DateTime CapturedAtUtc,
        string Source
    );

    /// <summary>Extension methods for mapping DeviceConfigSnapshot entities to/from DeviceConfigSnapshotDtos.</summary>
    public static class DeviceConfigSnapshotDtoMapping
    {
        /// <summary>
        /// Converts a DeviceConfigSnapshot entity to a DeviceConfigSnapshotDto.
        /// </summary>
        public static DeviceConfigSnapshotDto ToDto(this VideoForensics.Data.Common.Entities.DeviceConfigSnapshot entity)
        {
            return new DeviceConfigSnapshotDto(
                Id: entity.Id,
                DeviceId: entity.DeviceId,
                MotionDetectionEnabled: entity.MotionDetectionEnabled,
                MotionSensitivity: entity.MotionSensitivity,
                RecordingMode: entity.RecordingMode,
                CustomSettings: entity.ToCustomSettingsDictionary(),
                CapturedAtUtc: entity.CapturedAtUtc,
                Source: entity.Source.ToString()
            );
        }

        /// <summary>
        /// Converts a DeviceConfigSnapshotDto to a DeviceConfigSnapshot entity.
        /// </summary>
        public static VideoForensics.Data.Common.Entities.DeviceConfigSnapshot ToDomain(this DeviceConfigSnapshotDto dto)
        {
            return new VideoForensics.Data.Common.Entities.DeviceConfigSnapshot
            {
                Id = dto.Id,
                DeviceId = dto.DeviceId,
                MotionDetectionEnabled = dto.MotionDetectionEnabled,
                MotionSensitivity = dto.MotionSensitivity,
                RecordingMode = dto.RecordingMode,
                CustomSettings = ToCustomSettingRows(dto.CustomSettings),
                CapturedAtUtc = dto.CapturedAtUtc,
                Source = (VideoForensics.Data.Common.Entities.DeviceConfigSource)Enum.Parse(typeof(VideoForensics.Data.Common.Entities.DeviceConfigSource), dto.Source)
            };
        }

        /// <summary>Builds the custom settings object for a snapshot. A snapshot with no setting rows gives an empty object.</summary>
        public static Dictionary<string, JsonElement> ToCustomSettingsDictionary(this VideoForensics.Data.Common.Entities.DeviceConfigSnapshot entity)
        {
            return entity.CustomSettings.ToDictionary(
                s => s.Key,
                s => JsonSerializer.Deserialize<JsonElement>(s.ValueJson),
                StringComparer.Ordinal);
        }

        /// <summary>Builds one setting row per key. A null object gives no rows.</summary>
        public static List<VideoForensics.Data.Common.Entities.DeviceConfigSnapshotSetting> ToCustomSettingRows(Dictionary<string, JsonElement>? settings)
        {
            return (settings ?? []).Select(kv => new VideoForensics.Data.Common.Entities.DeviceConfigSnapshotSetting
            {
                Id = Guid.NewGuid(),
                Key = kv.Key,
                ValueJson = kv.Value.GetRawText()
            }).ToList();
        }
    }
}
