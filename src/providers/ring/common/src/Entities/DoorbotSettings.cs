using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

using VideoForensics.Core.Helpers.Json.Converters;

namespace VideoForensics.Providers.Ring.Entities
{
    /// <summary>The "settings" object of a doorbot in ring_devices. Keys we do not model are kept in AdditionalFields.</summary>
    public class DoorbotSettings
    {
        [JsonPropertyName("doorbell_volume")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public long? DoorbellVolume { get; set; }

        [JsonPropertyName("night_mode_on")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? NightModeOn { get; set; }

        [JsonPropertyName("motion_detection_enabled")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? MotionDetectionEnabled { get; set; }

        /// <summary>Ring sends 0 or 1. Also mapped onto Devices for doorbots and stickup cams.</summary>
        [JsonPropertyName("enable_vod")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonConverter(typeof(FlexibleBooleanConverter))]
        public bool? EnableVod { get; set; }

        [JsonPropertyName("exposure_control")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public long? ExposureControl { get; set; }

        /// <summary>Ring sends 0 or 1.</summary>
        [JsonPropertyName("vod_suspended")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public long? VodSuspended { get; set; }

        [JsonPropertyName("pir_sensitivity_1")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public long? PirSensitivity1 { get; set; }

        [JsonPropertyName("enable_audio_recording")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? EnableAudioRecording { get; set; }

        [JsonPropertyName("advanced_motion_detection_enabled")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? AdvancedMotionDetectionEnabled { get; set; }

        [JsonPropertyName("advanced_motion_detection_human_only_mode")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? AdvancedMotionDetectionHumanOnlyMode { get; set; }

        [JsonPropertyName("live_view_preset_profile")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string LiveViewPresetProfile { get; set; }

        /// <summary>The top-level key. When absent, the mapper falls back to settings.motion_settings.motion_snooze_preset_profile.</summary>
        [JsonPropertyName("motion_snooze_preset_profile")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string MotionSnoozePresetProfile { get; set; }

        [JsonPropertyName("chime_settings")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public DoorbotChimeSettings ChimeSettings { get; set; }

        [JsonPropertyName("advanced_object_settings")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public AdvancedObjectSettings AdvancedObjectSettings { get; set; }

        /// <summary>Settings keys this model does not name. Stored as settings metadata JSON so no Ring value is dropped.</summary>
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? AdditionalFields { get; set; }
    }
}
