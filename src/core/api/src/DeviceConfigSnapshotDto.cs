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
}
