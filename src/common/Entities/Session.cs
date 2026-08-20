using System.Text.Json.Serialization;

namespace Ring.Api.Entities
{
    public class Session
    {
        [JsonPropertyName("profile")]
        public Profile Profile { get; set; }
    }
}
