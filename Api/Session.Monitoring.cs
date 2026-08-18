using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace KoenZomers.Ring.Api
{
    /// <summary>
    /// Alarm account monitoring status, panic-button style alarm triggering, and location-wide
    /// history. Response shapes are not confirmed against a live capture, so status/history are
    /// returned as raw JsonElement rather than an invented strongly-typed shape.
    /// </summary>
    public partial class Session
    {
        /// <summary>
        /// Base Uri for the Ring Alarm monitoring accounts API.
        /// </summary>
        public Uri RingMonitoringApiBaseUrl => new Uri("https://api.ring.com/rs/monitoring/accounts/");

        /// <summary>
        /// Base Uri for Ring's "rs" history API (location-wide, distinct from clients_api/doorbots/history).
        /// </summary>
        public Uri RingRsApiBaseUrl => new Uri("https://api.ring.com/rs/");

        /// <summary>
        /// Returns the alarm monitoring status for a location's account.
        /// </summary>
        /// <param name="locationId">ID of the location to retrieve monitoring status for</param>
        public async Task<JsonElement> GetAccountMonitoringStatus(Guid locationId)
        {
            await EnsureSessionValid();

            var uri = new Uri(RingMonitoringApiBaseUrl, $"{locationId:D}");
            var response = await _httpUtility.GetContents(uri, AuthenticationToken, _hardwareId);

            return JsonDocument.Parse(response).RootElement.Clone();
        }

        /// <summary>
        /// Triggers a panic/user alarm for a specific monitored asset at a location. This causes a
        /// real alarm event - only call with explicit intent.
        /// </summary>
        /// <param name="locationId">ID of the location the asset belongs to</param>
        /// <param name="assetUuid">UUID of the monitored asset to trigger the alarm for</param>
        public async Task TriggerAlarm(Guid locationId, string assetUuid)
        {
            if (string.IsNullOrEmpty(assetUuid))
            {
                throw new ArgumentNullException(nameof(assetUuid));
            }

            await EnsureSessionValid();

            var uri = new Uri(RingMonitoringApiBaseUrl, $"{locationId:D}/assets/{assetUuid}/userAlarm");
            await _httpUtility.SendRequestWithExpectedStatusOutcome(uri, System.Net.Http.HttpMethod.Post, null, null, AuthenticationToken);
        }

        /// <summary>
        /// Returns raw location-wide history entries (distinct from the per-doorbot
        /// GetDoorbotsHistory()/GetLocationEvents() feeds).
        /// </summary>
        /// <param name="locationId">ID of the location to retrieve history for</param>
        public async Task<JsonElement> GetLocationHistory(Guid locationId)
        {
            await EnsureSessionValid();

            var uri = new Uri(RingRsApiBaseUrl, $"history?locationId={locationId:D}");
            var response = await _httpUtility.GetContents(uri, AuthenticationToken, _hardwareId);

            return JsonDocument.Parse(response).RootElement.Clone();
        }
    }
}
