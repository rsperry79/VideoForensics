using System;
using System.Text.Json;
using System.Threading.Tasks;

using Ring.Api.Entities;

namespace Ring.Api
{
    /// <summary>
    /// Location Mode - Ring's REST-based "home/away/disarmed" feature for camera-only locations
    /// without an Alarm hub. NOT full Ring Alarm security-panel control (that uses a persistent
    /// device-command websocket and is out of scope for this REST-only wrapper).
    /// </summary>
    public partial class Session
    {
        /// <summary>
        /// Base Uri for the Ring Location Mode API.
        /// </summary>
        public Uri RingModeApiBaseUrl => new Uri("https://api.ring.com/rs/mode/");

        /// <summary>
        /// Returns the current Location Mode ("home", "away" or "disarmed") for the given location.
        /// Only applicable to locations without a Ring Alarm hub - locations with a hub use full
        /// Alarm security-panel state instead, which this method does not cover.
        /// </summary>
        /// <param name="locationId">ID of the location to retrieve the mode for</param>
        /// <exception cref="Exceptions.AuthenticationFailedException">Thrown when the refresh token is invalid.</exception>
        /// <exception cref="Exceptions.SessionNotAuthenticatedException">Thrown when there's no OAuth token, or the OAuth token has expired and there is no valid refresh token.</exception>
        /// <exception cref="Exceptions.ThrottledException">Thrown when the web server indicates too many requests have been made (HTTP 429).</exception>
        public async Task<LocationMode> GetLocationMode(Guid locationId)
        {
            await EnsureSessionValid();

            var uri = new Uri(RingModeApiBaseUrl, $"location/{locationId:D}");
            var response = await _httpUtility.GetContents(uri, AuthenticationToken, _hardwareId);

            return JsonSerializer.Deserialize<LocationMode>(response);
        }

        /// <summary>
        /// Sets the Location Mode for the given location.
        /// </summary>
        /// <param name="locationId">ID of the location to set the mode for</param>
        /// <param name="mode">Mode to set: "home", "away" or "disarmed"</param>
        /// <exception cref="Exceptions.AuthenticationFailedException">Thrown when the refresh token is invalid.</exception>
        /// <exception cref="Exceptions.SessionNotAuthenticatedException">Thrown when there's no OAuth token, or the OAuth token has expired and there is no valid refresh token.</exception>
        /// <exception cref="Exceptions.ThrottledException">Thrown when the web server indicates too many requests have been made (HTTP 429).</exception>
        public async Task SetLocationMode(Guid locationId, string mode)
        {
            if (string.IsNullOrEmpty(mode))
            {
                throw new ArgumentNullException(nameof(mode));
            }

            await EnsureSessionValid();

            var uri = new Uri(RingModeApiBaseUrl, $"location/{locationId:D}");
            var bodyContent = JsonSerializer.Serialize(new { mode });
            await _httpUtility.SendRequestWithExpectedStatusOutcome(uri, System.Net.Http.HttpMethod.Post, null, bodyContent, AuthenticationToken);
        }

        /// <summary>
        /// Returns the Location Mode settings (e.g. per-mode alert configuration) for a location.
        /// Response shape is not confirmed against a live capture, hence raw JsonElement rather
        /// than an invented type.
        /// </summary>
        /// <param name="locationId">ID of the location to retrieve mode settings for</param>
        public async Task<System.Text.Json.JsonElement> GetLocationModeSettings(Guid locationId)
        {
            await EnsureSessionValid();

            var uri = new Uri(RingModeApiBaseUrl, $"location/{locationId:D}/settings");
            var response = await _httpUtility.GetContents(uri, AuthenticationToken, _hardwareId);

            return System.Text.Json.JsonDocument.Parse(response).RootElement.Clone();
        }

        /// <summary>
        /// Overwrites the Location Mode settings for a location. Caller is responsible for
        /// constructing a valid settings payload (e.g. from a prior GetLocationModeSettings() call).
        /// </summary>
        /// <param name="locationId">ID of the location to set mode settings for</param>
        /// <param name="settingsJson">Raw JSON body describing the settings to apply</param>
        public async Task SetLocationModeSettings(Guid locationId, string settingsJson)
        {
            await EnsureSessionValid();

            var uri = new Uri(RingModeApiBaseUrl, $"location/{locationId:D}/settings");
            await _httpUtility.SendRequestWithExpectedStatusOutcome(uri, System.Net.Http.HttpMethod.Post, null, settingsJson, AuthenticationToken);
        }

        /// <summary>
        /// Enables Location Modes for a location that doesn't have them set up yet.
        /// </summary>
        /// <param name="locationId">ID of the location to enable Location Modes for</param>
        public async Task EnableLocationModes(Guid locationId)
        {
            await EnsureSessionValid();

            var uri = new Uri(RingModeApiBaseUrl, $"location/{locationId:D}/settings/setup");
            await _httpUtility.SendRequestWithExpectedStatusOutcome(uri, System.Net.Http.HttpMethod.Post, null, null, AuthenticationToken);
        }

        /// <summary>
        /// Disables Location Modes for a location.
        /// </summary>
        /// <param name="locationId">ID of the location to disable Location Modes for</param>
        public async Task DisableLocationModes(Guid locationId)
        {
            await EnsureSessionValid();

            var uri = new Uri(RingModeApiBaseUrl, $"location/{locationId:D}/settings");
            await _httpUtility.SendRequestWithExpectedStatusOutcome(uri, System.Net.Http.HttpMethod.Delete, null, null, AuthenticationToken);
        }

        /// <summary>
        /// Returns the Location Mode sharing configuration for a location. Response shape is not
        /// confirmed against a live capture, hence raw JsonElement rather than an invented type.
        /// </summary>
        /// <param name="locationId">ID of the location to retrieve mode sharing settings for</param>
        public async Task<System.Text.Json.JsonElement> GetLocationModeSharing(Guid locationId)
        {
            await EnsureSessionValid();

            var uri = new Uri(RingModeApiBaseUrl, $"location/{locationId:D}/sharing");
            var response = await _httpUtility.GetContents(uri, AuthenticationToken, _hardwareId);

            return System.Text.Json.JsonDocument.Parse(response).RootElement.Clone();
        }

        /// <summary>
        /// Overwrites the Location Mode sharing configuration for a location.
        /// </summary>
        /// <param name="locationId">ID of the location to set mode sharing settings for</param>
        /// <param name="sharingJson">Raw JSON body describing the sharing settings to apply</param>
        public async Task SetLocationModeSharing(Guid locationId, string sharingJson)
        {
            await EnsureSessionValid();

            var uri = new Uri(RingModeApiBaseUrl, $"location/{locationId:D}/sharing");
            await _httpUtility.SendRequestWithExpectedStatusOutcome(uri, System.Net.Http.HttpMethod.Post, null, sharingJson, AuthenticationToken);
        }
    }
}
