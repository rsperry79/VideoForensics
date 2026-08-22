using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace VideoForensics.Providers.Ring
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
        public Uri MonitoringApiBaseUrl => new Uri("https://api.ring.com/rs/monitoring/accounts/");

        /// <summary>
        /// Base Uri for Ring's event-manager history API (location-wide, distinct from
        /// clients_api/doorbots/history). Confirmed against python-ring-doorbell's const.py
        /// (LOCATIONS_HISTORY_ENDPOINT) after the originally-guessed rs/history?locationId= path
        /// 404'd in a live ApiTester run.
        /// </summary>
        public Uri RingEvmApiBaseUrl => new Uri("https://api.ring.com/evm/v2/history/locations/");

        /// <summary>
        /// Returns the alarm monitoring status for a location's account.
        /// </summary>
        /// <param name="locationId">ID of the location to retrieve monitoring status for</param>
        public async Task<JsonElement> GetAccountMonitoringStatus(Guid locationId, CancellationToken cancellationToken = default)
        {
            await EnsureSessionValid(cancellationToken);

            var uri = new Uri(MonitoringApiBaseUrl, $"{locationId:D}");
            var response = await _httpUtility.GetContents(uri, AuthenticationToken, _hardwareId, cancellationToken);

            return JsonDocument.Parse(response).RootElement.Clone();
        }

        /// <summary>
        /// Triggers a panic/user alarm for a specific monitored asset at a location. This causes a
        /// real alarm event - only call with explicit intent.
        /// </summary>
        /// <param name="locationId">ID of the location the asset belongs to</param>
        /// <param name="assetUuid">UUID of the monitored asset to trigger the alarm for</param>
        public async Task TriggerAlarm(Guid locationId, string assetUuid, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(assetUuid))
            {
                throw new ArgumentNullException(nameof(assetUuid));
            }

            await EnsureSessionValid(cancellationToken);

            var uri = new Uri(MonitoringApiBaseUrl, $"{locationId:D}/assets/{assetUuid}/userAlarm");
            await _httpUtility.SendRequestWithExpectedStatusOutcome(uri, System.Net.Http.HttpMethod.Post, null, null, AuthenticationToken, cancellationToken);
        }

        /// <summary>
        /// Returns raw location-wide history entries (distinct from the per-doorbot
        /// GetDoorbotsHistory()/GetLocationEvents() feeds).
        /// </summary>
        /// <param name="locationId">ID of the location to retrieve history for</param>
        public async Task<JsonElement> GetLocationHistory(Guid locationId, CancellationToken cancellationToken = default)
        {
            await EnsureSessionValid(cancellationToken);

            var uri = new Uri(RingEvmApiBaseUrl, $"{locationId:D}");
            var response = await _httpUtility.GetContents(uri, AuthenticationToken, _hardwareId, cancellationToken);

            return JsonDocument.Parse(response).RootElement.Clone();
        }
    }
}
