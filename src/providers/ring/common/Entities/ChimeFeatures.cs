using System.Text.Json.Serialization;

namespace VideoForensics.Providers.Ring.Entities
{
    public class ChimeFeatures
    {
        [JsonPropertyName("ringtones_enabled")]
        public bool RingtonesEnabled { get; set; }
    }
}
