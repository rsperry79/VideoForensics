using System.Text.Json.Serialization;

namespace VideoForensics.Providers.Ring.Entities
{
    public class DoorbotHistoryEventRecording
    {
        [JsonPropertyName("status")]
        public string Status { get; set; }
    }
}
