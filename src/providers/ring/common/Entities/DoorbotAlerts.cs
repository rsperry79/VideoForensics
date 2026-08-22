using System.Text.Json.Serialization;

namespace VideoForensics.Providers.Ring.Entities
{
    public class StickupCamAlerts
    {
        [JsonPropertyName("connection")]
        public string Connection { get; set; }
    }
}
