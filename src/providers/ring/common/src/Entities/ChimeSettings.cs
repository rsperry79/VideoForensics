using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VideoForensics.Providers.Ring.Entities
{
    public class ChimeSettings
    {
        /// <summary>Settings keys this model does not name (for example status_led_enable). Read by the settings parser; not stored as JSON.</summary>
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? AdditionalFields { get; set; }

        [JsonPropertyName("volume")]
        public int Volume { get; set; }

        [JsonPropertyName("ding_audio_user_id")]
        public string DingAudioUserId { get; set; }

        [JsonPropertyName("ding_audio_id")]
        public string DingAudioId { get; set; }

        [JsonPropertyName("motion_audio_user_id")]
        public string MotionAudioUserId { get; set; }

        [JsonPropertyName("motion_audio_id")]
        public string MotionAudioId { get; set; }
    }
}
