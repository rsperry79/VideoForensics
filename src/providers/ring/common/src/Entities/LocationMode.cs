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
    }
}
