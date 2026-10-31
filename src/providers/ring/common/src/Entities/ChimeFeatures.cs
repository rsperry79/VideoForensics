using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VideoForensics.Providers.Ring.Entities
{
    public class ChimeFeatures
    {
        [JsonPropertyName("ringtones_enabled")]
        public bool RingtonesEnabled { get; set; }

        /// <summary>
        /// Every feature key without a dedicated property above (the feature objects and flag scalars of the ring_devices
        /// <c>features</c> object), kept as parsed JSON so none is dropped.
        /// </summary>
        [JsonExtensionData]
        public Dictionary<string, JsonElement> AdditionalFeatures { get; set; } = [];
    }
}
