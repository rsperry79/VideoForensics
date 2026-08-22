using System.Text.Json.Serialization;

namespace VideoForensics.Providers.Ring.Entities
{
    public class ChimeAlerts
    {
        [JsonPropertyName("connection")]
        public string Connection { get; set; }
    }
}
