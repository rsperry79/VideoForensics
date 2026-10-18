using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VideoForensics.Providers.Ring.Entities
{
    /// <summary>
    /// Response shape of GET/POST https://api.ring.com/rs/mode/location/{locationId} - Ring's
    /// lighter-weight "home/away/disarmed" mode for camera-only locations without an Alarm hub.
    /// This is distinct from full Ring Alarm security-panel arm/disarm, which uses a persistent
    /// device-command websocket instead of this REST endpoint.
    /// </summary>
    public class LocationMode
    {
        /// <summary>
        /// Current mode: "home", "away" or "disarmed"
        /// </summary>
        [JsonPropertyName("mode")]
        public string Mode { get; set; }

        [JsonPropertyName("lastUpdateTimeMS")]
        public long? LastUpdateTimeMs { get; set; }

        [JsonPropertyName("securityStatus")]
        public LocationModeSecurityStatus SecurityStatus { get; set; }

        [JsonPropertyName("readOnly")]
        public bool? ReadOnly { get; set; }

        /// <summary>Element shape is not captured yet (empty in every capture), so each element is kept as raw JSON.</summary>
        [JsonPropertyName("notYetParticipatingInMode")]
        public List<JsonElement> NotYetParticipatingInMode { get; set; }

        [JsonPropertyName("modePanelType")]
        public string ModePanelType { get; set; }
    }

    /// <summary>Security status block of a location mode response.</summary>
    public class LocationModeSecurityStatus
    {
        /// <summary>Last update time in milliseconds since the epoch.</summary>
        [JsonPropertyName("lu")]
        public long? LastUpdateTimeMs { get; set; }

        /// <summary>Mode the security status was last set to.</summary>
        [JsonPropertyName("md")]
        public string Mode { get; set; }
    }
}
