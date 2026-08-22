using System.Text.Json.Serialization;

namespace VideoForensics.Providers.Ring.Entities
{
    public class LightSettings
    {
        [JsonPropertyName("brightness")]
        public long? Brightness { get; set; }
    }
}
