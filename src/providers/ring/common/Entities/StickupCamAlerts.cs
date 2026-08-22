using System.Text.Json.Serialization;

namespace VideoForensics.Providers.Ring.Entities
{
    public class DoorbotAlerts
    {
        [JsonPropertyName("connection")]
        public string Connection { get; set; }
    }
}
