using System.Text.Json.Serialization;

namespace Ring.Api.Entities
{
    public class LightSettings
    {
        [JsonPropertyName("brightness")]
        public long? Brightness { get; set; }
    }
}
