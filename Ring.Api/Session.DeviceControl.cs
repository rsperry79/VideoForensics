using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace KoenZomers.Ring.Api
{
    /// <summary>
    /// Device control and settings: light, siren, chime test sound, volume, motion detection,
    /// chime type, do-not-disturb and night mode. All simple REST PUT/PATCH/POST calls.
    /// </summary>
    public partial class Session
    {
        /// <summary>
        /// Turns the floodlight/spotlight on a Ring camera on or off. Only applicable to devices with a light
        /// (floodlight cams, spotlight cams). Uses the same endpoint path other Ring API clients (e.g.
        /// python-ring-doorbell, ring-client-api) use: PUT clients_api/doorbots/{id}/floodlight_light_{on|off}
        /// </summary>
        /// <param name="doorbotId">ID of the device to control the light of</param>
        /// <param name="on">True to turn the light on, false to turn it off</param>
        /// <exception cref="Exceptions.AuthenticationFailedException">Thrown when the refresh token is invalid.</exception>
        /// <exception cref="Exceptions.SessionNotAuthenticatedException">Thrown when there's no OAuth token, or the OAuth token has expired and there is no valid refresh token.</exception>
        /// <exception cref="Exceptions.ThrottledException">Thrown when the web server indicates too many requests have been made (HTTP 429).</exception>
        /// <exception cref="Exceptions.DeviceUnknownException">Thrown when the web server indicates the requested Ring device was not found (HTTP 404).</exception>
        public async Task SetLight(long doorbotId, bool on, CancellationToken cancellationToken = default)
        {
            await EnsureSessionValid(cancellationToken);

            var uri = new Uri(BaseUrl, $"doorbots/{doorbotId}/floodlight_light_{(on ? "on" : "off")}");
            await _httpUtility.SendRequestWithExpectedStatusOutcome(uri, System.Net.Http.HttpMethod.Put, null, null, AuthenticationToken, cancellationToken);
        }

        /// <summary>
        /// Turns the siren on a Ring camera on or off. Uses the same endpoint path other Ring API clients use:
        /// PUT clients_api/doorbots/{id}/siren_{on|off}
        /// </summary>
        /// <param name="doorbotId">ID of the device to control the siren of</param>
        /// <param name="on">True to turn the siren on, false to turn it off</param>
        /// <param name="durationSeconds">Optional duration in seconds to sound the siren for when turning it on. Leave NULL to let Ring use its default.</param>
        /// <exception cref="Exceptions.AuthenticationFailedException">Thrown when the refresh token is invalid.</exception>
        /// <exception cref="Exceptions.SessionNotAuthenticatedException">Thrown when there's no OAuth token, or the OAuth token has expired and there is no valid refresh token.</exception>
        /// <exception cref="Exceptions.ThrottledException">Thrown when the web server indicates too many requests have been made (HTTP 429).</exception>
        /// <exception cref="Exceptions.DeviceUnknownException">Thrown when the web server indicates the requested Ring device was not found (HTTP 404).</exception>
        public async Task SetSiren(long doorbotId, bool on, int? durationSeconds = null, CancellationToken cancellationToken = default)
        {
            await EnsureSessionValid(cancellationToken);

            var path = $"doorbots/{doorbotId}/siren_{(on ? "on" : "off")}";
            if (on && durationSeconds.HasValue)
            {
                path += $"?duration={durationSeconds.Value}";
            }

            var uri = new Uri(BaseUrl, path);
            await _httpUtility.SendRequestWithExpectedStatusOutcome(uri, System.Net.Http.HttpMethod.Put, null, null, AuthenticationToken, cancellationToken);
        }

        /// <summary>
        /// Plays a test sound on a Ring chime. Uses the same endpoint path other Ring API clients use:
        /// POST clients_api/chimes/{id}/play_sound
        /// </summary>
        /// <param name="chimeId">ID of the chime to play the test sound on</param>
        /// <param name="kind">Sound to play - "ding" (default doorbell sound) or "motion"</param>
        /// <exception cref="Exceptions.AuthenticationFailedException">Thrown when the refresh token is invalid.</exception>
        /// <exception cref="Exceptions.SessionNotAuthenticatedException">Thrown when there's no OAuth token, or the OAuth token has expired and there is no valid refresh token.</exception>
        /// <exception cref="Exceptions.ThrottledException">Thrown when the web server indicates too many requests have been made (HTTP 429).</exception>
        /// <exception cref="Exceptions.DeviceUnknownException">Thrown when the web server indicates the requested Ring device was not found (HTTP 404).</exception>
        public async Task TestChimeSound(int chimeId, string kind = "ding", CancellationToken cancellationToken = default)
        {
            await EnsureSessionValid(cancellationToken);

            var uri = new Uri(BaseUrl, $"chimes/{chimeId}/play_sound");
            var bodyContent = JsonSerializer.Serialize(new { kind });
            await _httpUtility.SendRequestWithExpectedStatusOutcome(uri, System.Net.Http.HttpMethod.Post, null, bodyContent, AuthenticationToken, cancellationToken);
        }

        /// <summary>
        /// Sets the ringtone/notification volume of a doorbell or camera. Mirrors python-ring-doorbell's
        /// async_set_volume: PUT clients_api/doorbots/{id} with the "doorbot[settings][doorbell_volume]"
        /// flattened key Ring's legacy clients_api expects.
        /// </summary>
        /// <param name="doorbotId">ID of the device to set the volume of</param>
        /// <param name="volume">Volume level, 0-11 depending on device model</param>
        /// <exception cref="Exceptions.AuthenticationFailedException">Thrown when the refresh token is invalid.</exception>
        /// <exception cref="Exceptions.SessionNotAuthenticatedException">Thrown when there's no OAuth token, or the OAuth token has expired and there is no valid refresh token.</exception>
        /// <exception cref="Exceptions.ThrottledException">Thrown when the web server indicates too many requests have been made (HTTP 429).</exception>
        /// <exception cref="Exceptions.DeviceUnknownException">Thrown when the web server indicates the requested Ring device was not found (HTTP 404).</exception>
        public async Task SetVolume(long doorbotId, int volume, CancellationToken cancellationToken = default)
        {
            await EnsureSessionValid(cancellationToken);

            var uri = new Uri(BaseUrl, $"doorbots/{doorbotId}");
            var bodyContent = JsonSerializer.Serialize(new System.Collections.Generic.Dictionary<string, object>
            {
                ["doorbot[settings][doorbell_volume]"] = volume
            });
            await _httpUtility.SendRequestWithExpectedStatusOutcome(uri, System.Net.Http.HttpMethod.Put, null, bodyContent, AuthenticationToken, cancellationToken);
        }

        /// <summary>
        /// Enables or disables motion detection on a doorbell or camera. Mirrors python-ring-doorbell's
        /// async_set_motion_detection: PATCH devices/v1/devices/{id}/settings
        /// </summary>
        /// <param name="doorbotId">ID of the device to toggle motion detection on</param>
        /// <param name="enabled">True to enable motion detection, false to disable it</param>
        /// <exception cref="Exceptions.AuthenticationFailedException">Thrown when the refresh token is invalid.</exception>
        /// <exception cref="Exceptions.SessionNotAuthenticatedException">Thrown when there's no OAuth token, or the OAuth token has expired and there is no valid refresh token.</exception>
        /// <exception cref="Exceptions.ThrottledException">Thrown when the web server indicates too many requests have been made (HTTP 429).</exception>
        /// <exception cref="Exceptions.DeviceUnknownException">Thrown when the web server indicates the requested Ring device was not found (HTTP 404).</exception>
        public async Task SetMotionDetection(long doorbotId, bool enabled, CancellationToken cancellationToken = default)
        {
            await EnsureSessionValid(cancellationToken);

            var uri = new Uri(RingDevicesApiBaseUrl, $"devices/{doorbotId}/settings");
            var bodyContent = JsonSerializer.Serialize(new
            {
                motion_settings = new { motion_detection_enabled = enabled }
            });
            await _httpUtility.SendRequestWithExpectedStatusOutcome(uri, System.Net.Http.HttpMethod.Patch, null, bodyContent, AuthenticationToken, cancellationToken);
        }

        /// <summary>
        /// Sets the existing (mechanical/digital) chime type wired up alongside a Ring doorbell. Mirrors
        /// python-ring-doorbell's async_set_existing_doorbell_type family of calls: PUT clients_api/doorbots/{id}
        /// </summary>
        /// <param name="doorbotId">ID of the doorbell to set the chime type of</param>
        /// <param name="type">Chime type: 0 = Mechanical, 1 = Digital, 2 = Not Present</param>
        /// <param name="enabled">Whether the existing chime should ring. Leave NULL to not change this.</param>
        /// <param name="duration">Duration in seconds for a digital chime. Leave NULL to not change this.</param>
        /// <exception cref="Exceptions.AuthenticationFailedException">Thrown when the refresh token is invalid.</exception>
        /// <exception cref="Exceptions.SessionNotAuthenticatedException">Thrown when there's no OAuth token, or the OAuth token has expired and there is no valid refresh token.</exception>
        /// <exception cref="Exceptions.ThrottledException">Thrown when the web server indicates too many requests have been made (HTTP 429).</exception>
        /// <exception cref="Exceptions.DeviceUnknownException">Thrown when the web server indicates the requested Ring device was not found (HTTP 404).</exception>
        public async Task SetChimeType(long doorbotId, int type, bool? enabled = null, int? duration = null, CancellationToken cancellationToken = default)
        {
            await EnsureSessionValid(cancellationToken);

            var uri = new Uri(BaseUrl, $"doorbots/{doorbotId}");
            var fields = new System.Collections.Generic.Dictionary<string, object>
            {
                ["doorbot[settings][chime_settings][type]"] = type
            };
            if (enabled.HasValue)
            {
                fields["doorbot[settings][chime_settings][enable]"] = enabled.Value;
            }
            if (duration.HasValue)
            {
                fields["doorbot[settings][chime_settings][duration]"] = duration.Value;
            }

            var bodyContent = JsonSerializer.Serialize(fields);
            await _httpUtility.SendRequestWithExpectedStatusOutcome(uri, System.Net.Http.HttpMethod.Put, null, bodyContent, AuthenticationToken, cancellationToken);
        }

        /// <summary>
        /// Snoozes/mutes a chime's notifications for a given duration. Unlike the other setters in this file,
        /// this endpoint is inferred rather than confirmed against a reference implementation - verify the
        /// exact path/body against a live capture (see api_raw_responses.jsonl) before relying on it.
        /// </summary>
        /// <param name="chimeId">ID of the chime to snooze</param>
        /// <param name="seconds">Number of seconds to snooze notifications for</param>
        /// <exception cref="Exceptions.AuthenticationFailedException">Thrown when the refresh token is invalid.</exception>
        /// <exception cref="Exceptions.SessionNotAuthenticatedException">Thrown when there's no OAuth token, or the OAuth token has expired and there is no valid refresh token.</exception>
        /// <exception cref="Exceptions.ThrottledException">Thrown when the web server indicates too many requests have been made (HTTP 429).</exception>
        /// <exception cref="Exceptions.DeviceUnknownException">Thrown when the web server indicates the requested Ring device was not found (HTTP 404).</exception>
        public async Task SetDoNotDisturb(long chimeId, int seconds, CancellationToken cancellationToken = default)
        {
            await EnsureSessionValid(cancellationToken);

            var uri = new Uri(BaseUrl, $"chimes/{chimeId}/do_not_disturb");
            var bodyContent = JsonSerializer.Serialize(new { time = seconds });
            await _httpUtility.SendRequestWithExpectedStatusOutcome(uri, System.Net.Http.HttpMethod.Put, null, bodyContent, AuthenticationToken, cancellationToken);
        }

        /// <summary>
        /// Enables or disables night mode (forced infrared/black-and-white) on a camera.
        /// </summary>
        /// <param name="doorbotId">ID of the device to toggle night mode on</param>
        /// <param name="enabled">True to force night mode on, false to let the device decide automatically</param>
        /// <exception cref="Exceptions.AuthenticationFailedException">Thrown when the refresh token is invalid.</exception>
        /// <exception cref="Exceptions.SessionNotAuthenticatedException">Thrown when there's no OAuth token, or the OAuth token has expired and there is no valid refresh token.</exception>
        /// <exception cref="Exceptions.ThrottledException">Thrown when the web server indicates too many requests have been made (HTTP 429).</exception>
        /// <exception cref="Exceptions.DeviceUnknownException">Thrown when the web server indicates the requested Ring device was not found (HTTP 404).</exception>
        public async Task SetNightMode(long doorbotId, bool enabled, CancellationToken cancellationToken = default)
        {
            await EnsureSessionValid(cancellationToken);

            var uri = new Uri(BaseUrl, $"doorbots/{doorbotId}");
            var bodyContent = JsonSerializer.Serialize(new System.Collections.Generic.Dictionary<string, object>
            {
                ["doorbot[settings][night_mode]"] = enabled ? 1 : 0
            });
            await _httpUtility.SendRequestWithExpectedStatusOutcome(uri, System.Net.Http.HttpMethod.Put, null, bodyContent, AuthenticationToken, cancellationToken);
        }

        /// <summary>
        /// Returns the raw per-device settings object (motion zones, detection toggles, etc.) for a
        /// device. Shape varies by device type, hence raw JsonElement rather than an invented type -
        /// see SetMotionDetection/SetMotionZones for the specific sub-shapes this client already
        /// knows how to write.
        /// </summary>
        /// <param name="doorbotId">ID of the device to retrieve settings for</param>
        public async Task<System.Text.Json.JsonElement> GetDeviceSettings(long doorbotId, CancellationToken cancellationToken = default)
        {
            await EnsureSessionValid(cancellationToken);

            var uri = new Uri(RingDevicesApiBaseUrl, $"devices/{doorbotId}/settings");
            var response = await _httpUtility.GetContents(uri, AuthenticationToken, _hardwareId, cancellationToken);

            return System.Text.Json.JsonDocument.Parse(response).RootElement.Clone();
        }

        /// <summary>
        /// Overwrites a chime's settings (e.g. description, assigned ringtones). Caller is
        /// responsible for constructing a valid settings payload; body shape is not confirmed
        /// against a live capture.
        /// </summary>
        /// <param name="chimeId">ID of the chime to update</param>
        /// <param name="settingsJson">Raw JSON body describing the settings to apply</param>
        public async Task UpdateChime(long chimeId, string settingsJson, CancellationToken cancellationToken = default)
        {
            await EnsureSessionValid(cancellationToken);

            var uri = new Uri(BaseUrl, $"chimes/{chimeId}");
            await _httpUtility.SendRequestWithExpectedStatusOutcome(uri, System.Net.Http.HttpMethod.Put, null, settingsJson, AuthenticationToken, cancellationToken);
        }

        /// <summary>
        /// Returns the doorbots linked to (chiming through) a given chime. Endpoint path confirmed
        /// against python-ring-doorbell's const.py (LINKED_CHIMES_ENDPOINT); response shape not
        /// confirmed, hence raw JsonElement rather than an invented type.
        /// </summary>
        /// <param name="chimeId">ID of the chime to retrieve linked doorbots for</param>
        public async Task<System.Text.Json.JsonElement> GetLinkedChimeDoorbots(long chimeId, CancellationToken cancellationToken = default)
        {
            await EnsureSessionValid(cancellationToken);

            var uri = new Uri(BaseUrl, $"chimes/{chimeId}/linked_doorbots");
            var response = await _httpUtility.GetContents(uri, AuthenticationToken, _hardwareId, cancellationToken);

            return System.Text.Json.JsonDocument.Parse(response).RootElement.Clone();
        }
    }
}
