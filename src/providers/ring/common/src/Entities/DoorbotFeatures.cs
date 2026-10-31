using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VideoForensics.Providers.Ring.Entities
{
    public class DoorbotFeatures
    {
        [JsonPropertyName("motions_enabled")]
        public bool MotionsEnabled { get; set; }

        [JsonPropertyName("show_recordings")]
        public bool ShowRecordings { get; set; }

        [JsonPropertyName("advanced_motion_enabled")]
        public bool AdvancedMotionEnabled { get; set; }

        [JsonPropertyName("people_only_enabled")]
        public bool PeopleOnlyEnabled { get; set; }

        [JsonPropertyName("shadow_correction_enabled")]
        public bool ShadowCorrectionEnabled { get; set; }

        [JsonPropertyName("motion_message_enabled")]
        public bool MotionMessageEnabled { get; set; }

        [JsonPropertyName("night_vision_enabled")]
        public bool NightVisionEnabled { get; set; }

        [JsonPropertyName("show_vod_settings")]
        public bool? ShowVodSettings { get; set; }

        /// <summary>
        /// Every feature key without a dedicated property above (the feature objects and flag scalars of the ring_devices
        /// <c>features</c> object), kept as parsed JSON so none is dropped.
        /// </summary>
        [JsonExtensionData]
        public Dictionary<string, JsonElement> AdditionalFeatures { get; set; } = [];
    }
}
