using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace VideoForensics.Providers.Ring.Entities
{
    /// <summary>
    /// Timestamps related to a specific doorbot
    /// </summary>
    public class DoorbotTimestamps
    {
        /// <summary>
        /// Collection of doorbot timestamps
        /// </summary>
        [JsonPropertyName("timestamps")]
        public List<DoorbotTimestamp> Timestamp { get; set; }
    }
}
