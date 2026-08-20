using System.Text.Json.Serialization;

namespace Ring.Api.Entities
{
    public class ChimeAlerts
    {
        [JsonPropertyName("connection")]
        public string Connection { get; set; }
    }
}
