using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using KoenZomers.Ring.Api.Entities;

namespace KoenZomers.Ring.Api
{
    /// <summary>
    /// Unified event feed across a location's devices, as an alternative to per-doorbot
    /// GetDoorbotsHistory(). Endpoint paths mirror ring-client-api's getCameraEvents()/getEvents()
    /// - not confirmed against a live capture.
    /// </summary>
    public partial class Session
    {
        /// <summary>
        /// Returns recent events across every device at a location.
        /// </summary>
        /// <param name="locationId">ID of the location to retrieve events for</param>
        public async Task<List<DoorbotHistoryEvent>> GetLocationEvents(Guid locationId)
        {
            await EnsureSessionValid();

            var uri = new Uri(RingApiBaseUrl, $"locations/{locationId:D}/events");
            var response = await _httpUtility.GetContents(uri, AuthenticationToken, _hardwareId);

            var parsed = JsonSerializer.Deserialize<LocationEventsResponse>(response);
            return parsed?.Events ?? new List<DoorbotHistoryEvent>();
        }

        /// <summary>
        /// Returns recent events for a single device at a location.
        /// </summary>
        /// <param name="locationId">ID of the location the device belongs to</param>
        /// <param name="doorbotId">ID of the device to retrieve events for</param>
        public async Task<List<DoorbotHistoryEvent>> GetDeviceEvents(Guid locationId, long doorbotId)
        {
            await EnsureSessionValid();

            var uri = new Uri(RingApiBaseUrl, $"locations/{locationId:D}/devices/{doorbotId}/events");
            var response = await _httpUtility.GetContents(uri, AuthenticationToken, _hardwareId);

            var parsed = JsonSerializer.Deserialize<LocationEventsResponse>(response);
            return parsed?.Events ?? new List<DoorbotHistoryEvent>();
        }
    }
}
