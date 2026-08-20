using System.Text.Json.Serialization;

namespace Ring.Api.Entities
{
    public class DoorbotHistoryEventRecording
    {
        [JsonPropertyName("status")]
        public string Status { get; set; }
    }
}
