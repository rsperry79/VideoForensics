using System.Net.Http.Json;

using VideoForensics.Core.Api.Contracts;
using VideoForensics.Core.Providers.Common.Contracts;

namespace VideoForensics.Client.Hosting.Remote
{
    /// <summary>
    /// HTTP-backed <see cref="IDeviceControlService"/> that calls the server's device-control Minimal API
    /// (see VideoForensics.WebApp/Api/DeviceControlEndpoints.cs). The route id is the provider device id.
    /// The paired-device bearer token is attached by <see cref="PairedDeviceAuthHandler"/>, not here.
    /// Failures throw <see cref="HttpRequestException"/> with a message that omits the request URL.
    /// </summary>
    public class RemoteDeviceControlService : IDeviceControlService
    {
        private readonly HttpClient _httpClient;

        public RemoteDeviceControlService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <inheritdoc />
        public Task SetLightAsync(string providerDeviceId, bool on, CancellationToken ct)
        {
            return PostAsync(providerDeviceId, "light", new SetLightRequest(on), ct);
        }

        /// <inheritdoc />
        public Task SetSirenAsync(string providerDeviceId, bool on, int? durationSeconds, CancellationToken ct)
        {
            return PostAsync(providerDeviceId, "siren", new SetSirenRequest(on, durationSeconds), ct);
        }

        /// <inheritdoc />
        public Task SetVolumeAsync(string providerDeviceId, int volume, CancellationToken ct)
        {
            return PostAsync(providerDeviceId, "volume", new SetVolumeRequest(volume), ct);
        }

        /// <inheritdoc />
        public Task SetNightModeAsync(string providerDeviceId, bool enabled, CancellationToken ct)
        {
            return PostAsync(providerDeviceId, "night-mode", new SetNightModeRequest(enabled), ct);
        }

        /// <inheritdoc />
        public Task SetMotionDetectionAsync(string providerDeviceId, bool enabled, CancellationToken ct)
        {
            return PostAsync(providerDeviceId, "motion-detection", new SetMotionDetectionRequest(enabled), ct);
        }

        /// <inheritdoc />
        public Task SetDoNotDisturbAsync(string providerDeviceId, int seconds, CancellationToken ct)
        {
            return PostAsync(providerDeviceId, "do-not-disturb", new SetDoNotDisturbRequest(seconds), ct);
        }

        private async Task PostAsync<TBody>(string providerDeviceId, string action, TBody body, CancellationToken ct)
        {
            // Escape the id so a value containing '/' or '?' cannot change the route.
            string route = $"/api/v1/device-controls/{Uri.EscapeDataString(providerDeviceId)}/{action}";
            using HttpResponseMessage response = await _httpClient.PostAsJsonAsync(route, body, ct);
            if (!response.IsSuccessStatusCode)
            {
                // No request URL in the message: it carries the server address and the device id.
                throw new HttpRequestException(
                    $"Device control '{action}' failed with status {(int)response.StatusCode}.",
                    inner: null,
                    statusCode: response.StatusCode);
            }
        }
    }
}
