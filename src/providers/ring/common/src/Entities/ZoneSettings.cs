using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace VideoForensics.Providers.Ring.Entities
{
    /// <summary>The "zone_settings" object of a doorbot or stickup cam: motion zones and package zones.</summary>
    public class ZoneSettings
    {
        [JsonPropertyName("motion")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<RingZone> Motion { get; set; }

        [JsonPropertyName("package")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<RingZone> Package { get; set; }
    }
}
