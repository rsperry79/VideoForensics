using System.Text.Json.Serialization;

namespace VideoForensics.Providers.Ring.Entities
{
    public class Session
    {
        [JsonPropertyName("profile")]
        public Profile Profile { get; set; }
    }
}
