using System.Text.Json.Serialization;

namespace Ring.Api.Entities
{
    public class DoorbotAlerts
    {
        [JsonPropertyName("connection")]
        public string Connection { get; set; }
    }
}
