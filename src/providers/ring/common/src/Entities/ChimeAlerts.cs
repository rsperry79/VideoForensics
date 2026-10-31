using System.Text.Json.Serialization;

namespace VideoForensics.Providers.Ring.Entities
{
    public class ChimeAlerts
    {
        [JsonPropertyName("connection")]
        public string Connection { get; set; }

        /// <summary>alerts.ota_status (firmware update status text). Absent on some devices, so null is normal.</summary>
        [JsonPropertyName("ota_status")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? OtaStatus { get; set; }
    }
}
