using System;
using System.Text.Json;
using System.Threading.Tasks;

using VideoForensics.Providers.Ring.Entities;

namespace VideoForensics.Providers.Ring
{
    /// <summary>
    /// Motion zone configuration. Reading current zones already works via GetRingDevices() -
    /// StickupCamSettings.AdvancedMotionZones. This adds the setter.
    /// </summary>
    public partial class Session
    {
        /// <summary>
        /// Updates the motion zones configured on a camera. The body is advanced_motion_settings with zone_1..zone_3,
        /// the shape a live capture showed the Ring app writing (see <see cref="MotionZoneBody"/>), sent to the
        /// devices/v1/devices/{id}/settings endpoint.
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
            string bodyContent = MotionZoneBody.FromZones(zones);
            await _httpUtility.SendRequestWithExpectedStatusOutcome(uri, System.Net.Http.HttpMethod.Patch, null, bodyContent, AuthenticationToken);
        }

        /// <summary>
        /// Sends a raw PATCH to a device's settings endpoint. Used to probe which keys Ring accepts and whether a
        /// partial body merges or replaces the stored settings. The caller builds the whole body.
        /// </summary>
        /// <param name="doorbotId">ID of the device to patch</param>
        /// <param name="bodyJson">Raw JSON body to send</param>
        public async Task PatchDeviceSettingsRaw(long doorbotId, string bodyJson)
        {
            await EnsureSessionValid();

            var uri = new Uri(RingDevicesApiBaseUrl, $"devices/{doorbotId}/settings");
            await _httpUtility.SendRequestWithExpectedStatusOutcome(uri, System.Net.Http.HttpMethod.Patch, null, bodyJson, AuthenticationToken);
        }
    }
}
