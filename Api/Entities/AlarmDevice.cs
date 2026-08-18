using System.Text.Json.Serialization;

namespace KoenZomers.Ring.Api.Entities
{
    /// <summary>
    /// A device discovered on a location's asset socket (DeviceInfoDocGetList), flattened from
    /// Ring's "general.v2" + "device.v1" document shape - inferred from dgreif/ring's location.ts,
    /// not confirmed against a live capture (the test account has no Alarm hub to discover devices
    /// from). The security panel is the AlarmDevice where DeviceType == "security-panel".
    /// </summary>
    public class AlarmDevice
    {
        /// <summary>
        /// The device's own identifier within the location (referred to as "zid" in Ring's protocol).
        /// </summary>
        [JsonPropertyName("zid")]
        public string Zid { get; set; }

        [JsonPropertyName("deviceType")]
        public string DeviceType { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        /// <summary>
        /// Current alarm mode when this device is the security panel: "all" (armed away),
        /// "some" (armed home) or "none" (disarmed).
        /// </summary>
        [JsonPropertyName("mode")]
        public string Mode { get; set; }
    }
}
