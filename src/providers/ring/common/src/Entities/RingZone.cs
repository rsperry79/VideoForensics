using System.Collections.Generic;
using System.Text.Json.Serialization;

using VideoForensics.Core.Helpers.Json.Converters;

namespace VideoForensics.Providers.Ring.Entities
{
    /// <summary>One zone in zone_settings. Vertices are in Ring order.</summary>
    public class RingZone
    {
        [JsonPropertyName("id")]
        [JsonConverter(typeof(FlexibleStringConverter))]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Id { get; set; }

        [JsonPropertyName("name")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Name { get; set; }

        [JsonPropertyName("properties")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public RingZoneProperties Properties { get; set; }

        [JsonPropertyName("state")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public long? State { get; set; }

        [JsonPropertyName("vertices")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<Vertex> Vertices { get; set; }
    }
}
