using System;
using System.Text.Json;
using System.Threading.Tasks;

using KoenZomers.Ring.Api.Entities;

namespace KoenZomers.Ring.Api
{
    /// <summary>
    /// Motion zone configuration. Reading current zones already works via GetRingDevices() -
    /// StickupCamSettings.AdvancedMotionZones. This adds the setter.
    /// </summary>
    public partial class Session
    {
        /// <summary>
        /// Updates the motion zones configured on a camera. Neither python-ring-doorbell nor
        /// ring-client-api implement a zone setter to confirm against, so this endpoint/body shape is
        /// inferred from the sibling motion_detection_enabled PATCH pattern (same devices/v1/devices/{id}/settings
        /// endpoint Ring uses for other per-device settings). Verify against a live capture
        /// (api_raw_responses.jsonl, changing a zone through the Ring app) before relying on it.
        /// </summary>
        /// <param name="doorbotId">ID of the device to update motion zones for</param>
        /// <param name="zones">The motion zone configuration to apply</param>
        /// <exception cref="Exceptions.AuthenticationFailedException">Thrown when the refresh token is invalid.</exception>
        /// <exception cref="Exceptions.SessionNotAuthenticatedException">Thrown when there's no OAuth token, or the OAuth token has expired and there is no valid refresh token.</exception>
        /// <exception cref="Exceptions.ThrottledException">Thrown when the web server indicates too many requests have been made (HTTP 429).</exception>
        /// <exception cref="Exceptions.DeviceUnknownException">Thrown when the web server indicates the requested Ring device was not found (HTTP 404).</exception>
        public async Task SetMotionZones(long doorbotId, AdvancedMotionZones zones)
        {
            if (zones == null)
            {
                throw new ArgumentNullException(nameof(zones));
            }

            await EnsureSessionValid();

            var uri = new Uri(RingDevicesApiBaseUrl, $"devices/{doorbotId}/settings");
            var bodyContent = JsonSerializer.Serialize(new { motion_zones = zones });
            await _httpUtility.SendRequestWithExpectedStatusOutcome(uri, System.Net.Http.HttpMethod.Patch, null, bodyContent, AuthenticationToken);
        }
    }
}
