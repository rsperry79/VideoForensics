using System.Text.Json.Serialization;

namespace Ring.Api.Entities
{
    public class StickupCamAlerts
    {
        [JsonPropertyName("connection")]
        public string Connection { get; set; }
    }
}
