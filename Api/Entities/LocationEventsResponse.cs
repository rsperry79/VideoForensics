using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace KoenZomers.Ring.Api.Entities
{
    /// <summary>
    /// Response shape of GET https://api.ring.com/clients_api/locations/{locationId}/events and
    /// .../locations/{locationId}/devices/{deviceId}/events - a unified event feed across a
    /// location's devices, as an alternative to per-doorbot GetDoorbotsHistory(). Field shape
    /// mirrors ring-client-api's getCameraEvents()/getEvents() but is not confirmed against a
    /// live capture.
    /// </summary>
    public class LocationEventsResponse
    {
        [JsonPropertyName("events")]
        public List<DoorbotHistoryEvent> Events { get; set; }
    }
}
