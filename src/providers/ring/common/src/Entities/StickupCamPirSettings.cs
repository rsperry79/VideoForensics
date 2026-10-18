using System.Text.Json.Serialization;

namespace VideoForensics.Providers.Ring.Entities
{
    /// <summary>The "pir_settings" object inside a stickup cam's settings (passive infrared sensitivity and zone).</summary>
    public class StickupCamPirSettings
    {
        [JsonPropertyName("sensitivity_1")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public long? Sensitivity1 { get; set; }

        [JsonPropertyName("sensitivity_2")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public long? Sensitivity2 { get; set; }

        [JsonPropertyName("sensitivity_3")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public long? Sensitivity3 { get; set; }

        [JsonPropertyName("zone_enable")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public long? ZoneEnable { get; set; }

        [JsonPropertyName("zone_mask")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public long? ZoneMask { get; set; }
    }
}
