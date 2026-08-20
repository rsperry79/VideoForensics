using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace KoenZomers.Ring.Api
{
    /// <summary>
    /// Single-location detail lookup under clients_api, distinct from GetLocations() (which lists
    /// every location via the newer devices/v1/locations endpoint). Endpoint path matched
    /// python-ring-doorbell's const.py (LOCATIONS_ENDPOINT) at the source level, but a live
    /// ApiTester run (2026-08-19) got a 404 for every location on a real account - this endpoint
    /// appears to have been removed from Ring's backend in favor of devices/v1/locations, which
    /// already returns full Location objects (see GetLocations()) making this redundant even if it
    /// worked. Not called anywhere in RingVideos; kept for completeness but likely dead.
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

            var uri = new Uri(BaseUrl, $"locations/{locationId:D}");
            var response = await _httpUtility.GetContents(uri, AuthenticationToken, _hardwareId);

            return JsonDocument.Parse(response).RootElement.Clone();
        }
    }
}
