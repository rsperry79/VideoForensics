using System.Text.Json.Serialization;

namespace VideoForensics.Providers.Ring.Entities
{
    /// <summary>The "ignore_zones" object of a doorbot or stickup cam: zone1..zone4, each shaped like an advanced motion zone.</summary>
    public class IgnoreZones
    {
        [JsonPropertyName("zone1")]
        public Zone Zone1 { get; set; }

        [JsonPropertyName("zone2")]
        public Zone Zone2 { get; set; }

        [JsonPropertyName("zone3")]
        public Zone Zone3 { get; set; }

        [JsonPropertyName("zone4")]
        public Zone Zone4 { get; set; }
    }
}
