using System.Text.Json.Serialization;

namespace KoenZomers.Ring.Api.Entities
{
    /// <summary>
    /// Response shape of GET https://api.ring.com/clients_api/doorbots/{id}/health and
    /// .../chimes/{id}/health - the same connectivity/battery telemetry embedded in the devices
    /// listing (<see cref="DeviceHealth"/>), fetched standalone for a single device.
    /// </summary>
    public class DeviceHealthResponse
    {
        [JsonPropertyName("device_health")]
        public DeviceHealth DeviceHealth { get; set; }
    }
}
