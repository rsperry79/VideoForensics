using System.Text.Json.Serialization;

namespace VideoForensics.Providers.Ring.Entities
{
    public class HistoryEventRecording
    {
        [JsonPropertyName("status")]
        public string Status { get; set; }
    }
}
