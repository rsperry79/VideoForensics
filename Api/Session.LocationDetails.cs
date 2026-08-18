using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace KoenZomers.Ring.Api
{
    /// <summary>
    /// Single-location detail lookup under clients_api, distinct from GetLocations() (which lists
    /// every location via the newer devices/v1/locations endpoint). Endpoint path confirmed
    /// against python-ring-doorbell's const.py (LOCATIONS_ENDPOINT); response shape not confirmed,
    /// hence raw JsonElement rather than an invented type.
    /// </summary>
    public partial class Session
    {
        /// <summary>
        /// Returns details for a single location.
        /// </summary>
        /// <param name="locationId">ID of the location to retrieve</param>
        public async Task<JsonElement> GetLocation(Guid locationId)
        {
            await EnsureSessionValid();

            var uri = new Uri(RingApiBaseUrl, $"locations/{locationId:D}");
            var response = await _httpUtility.GetContents(uri, AuthenticationToken, _hardwareId);

            return JsonDocument.Parse(response).RootElement.Clone();
        }
    }
}
