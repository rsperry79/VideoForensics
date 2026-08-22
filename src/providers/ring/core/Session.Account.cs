using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

using VideoForensics.Providers.Ring.Entities;

namespace VideoForensics.Providers.Ring
{
    /// <summary>
    /// Account-level endpoints: profile, push notification receiver registration, chime ringtones,
    /// and the Amazon Key lock integration.
    /// </summary>
    public partial class Session
    {
        /// <summary>
        /// Returns the profile of the currently authenticated account.
        /// </summary>
        public async Task<Profile> GetProfile()
        {
            await EnsureSessionValid();

            var uri = new Uri(BaseUrl, "profile");
            var response = await _httpUtility.GetContents(uri, AuthenticationToken, _hardwareId);

            // Confirmed via a live ApiTester run: the response is wrapped in a top-level "profile"
            // key. Deserializing straight into Profile (as this originally did) silently produced
            // an all-null/default object every call, since "profile" doesn't match any Profile
            // property and System.Text.Json doesn't error on unmatched properties.
            return JsonSerializer.Deserialize<ProfileResponse>(response)?.Profile;
        }

        /// <summary>
        /// Registers a push notification receiver token for this account. Body shape mirrors
        /// ring-client-api's registerPushReceiver() - not confirmed against a live capture.
        /// </summary>
        /// <param name="deviceToken">Push notification token to register</param>
        /// <param name="operatingSystem">Operating system the token was issued for. Defaults to "android".</param>
        public async Task RegisterPushReceiver(string deviceToken, string operatingSystem = "android")
        {
            if (string.IsNullOrEmpty(deviceToken))
            {
                throw new ArgumentNullException(nameof(deviceToken));
            }

            await EnsureSessionValid();

            var uri = new Uri(BaseUrl, "device");
            var bodyContent = JsonSerializer.Serialize(new
            {
                device = new
                {
                    metadata = new { device_token = deviceToken },
                    os = operatingSystem
                }
            });
            await _httpUtility.SendRequestWithExpectedStatusOutcome(uri, System.Net.Http.HttpMethod.Patch, null, bodyContent, AuthenticationToken);
        }

        /// <summary>
        /// Returns the chime ringtones available to assign via UpdateChime().
        /// </summary>
        public async Task<List<Ringtone>> GetRingtones()
        {
            await EnsureSessionValid();

            var uri = new Uri(BaseUrl, "ringtones");
            var response = await _httpUtility.GetContents(uri, AuthenticationToken, _hardwareId);

            var parsed = JsonSerializer.Deserialize<RingtonesResponse>(response);
            return parsed?.Ringtones ?? new List<Ringtone>();
        }

        /// <summary>
        /// Base Uri for Ring's third-party integrations API.
        /// </summary>
        public Uri RingIntegrationsApiBaseUrl => new Uri("https://api.ring.com/integrations/");

        /// <summary>
        /// Returns the Amazon Key lock associations for this account. Response shape is not
        /// confirmed against a live capture, hence raw JsonElement rather than an invented type.
        /// </summary>
        public async Task<JsonElement> FetchAmazonKeyLocks()
        {
            await EnsureSessionValid();

            var uri = new Uri(RingIntegrationsApiBaseUrl, "amazonkey/v2/devices/lock_associations");
            var response = await _httpUtility.GetContents(uri, AuthenticationToken, _hardwareId);

            return JsonDocument.Parse(response).RootElement.Clone();
        }
    }
}
