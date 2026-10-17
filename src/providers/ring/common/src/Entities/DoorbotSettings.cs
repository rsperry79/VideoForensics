using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

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
