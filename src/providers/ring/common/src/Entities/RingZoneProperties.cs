using System.Collections.Generic;
using System.Text.Json.Serialization;

using VideoForensics.Core.Helpers.Json.Converters;

namespace VideoForensics.Providers.Ring.Entities
{
    /// <summary>The "properties" object of a zone in zone_settings.</summary>
    public class RingZoneProperties
    {
        [JsonPropertyName("detection_types")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<string> DetectionTypes { get; set; }

        [JsonPropertyName("style")]
        [JsonConverter(typeof(FlexibleStringConverter))]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string Style { get; set; }
    }
}
