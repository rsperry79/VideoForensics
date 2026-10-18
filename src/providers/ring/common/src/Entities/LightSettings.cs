using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VideoForensics.Providers.Ring.Entities
{
    public class LightSettings
    {
        [JsonPropertyName("brightness")]
        public long? Brightness { get; set; }

        /// <summary>Light keys this model does not name. Stored as light settings metadata JSON so no Ring value is dropped.</summary>
        [JsonExtensionData]
        public Dictionary<string, JsonElement>? AdditionalFields { get; set; }
    }
}
