using System.Net.Http.Json;
using System.Text.Json;

using VideoForensics.Api.Contracts;
using VideoForensics.Providers.Common.Contracts;

namespace VideoForensics.Hosting.Remote
{
    /// <summary>
    /// HTTP-backed <see cref="IDeviceDiscoveryService"/> that calls the server's Minimal API
    /// (see VideoForensics.WebApp/Api/DiscoveryEndpoints.cs) instead of using a provider SDK directly.
    /// This is the client-side implementation of the "thin client talks to a server API" half of the plan's
    /// client/server split (§4/M5).
    /// </summary>
    public class RemoteDeviceDiscoveryService : IDeviceDiscoveryService
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly HttpClient _httpClient;

        public RemoteDeviceDiscoveryService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<Location>> GetLocationsAsync(CancellationToken cancellationToken = default)
        {
            HttpResponseMessage response = await _httpClient.GetAsync("/api/v1/discovery/locations", cancellationToken);
            _ = response.EnsureSuccessStatusCode();
            List<LocationDto>? dtos = await response.Content.ReadFromJsonAsync<List<LocationDto>>(JsonOptions, cancellationToken);
            return (dtos ?? []).Select(x => x.ToDomain()).ToList();
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<Device>> GetDevicesAsync(string locationId, CancellationToken cancellationToken = default)
        {
            HttpResponseMessage response = await _httpClient.GetAsync($"/api/v1/discovery/locations/{locationId}/devices", cancellationToken);
            _ = response.EnsureSuccessStatusCode();
            List<DiscoveryDeviceDto>? dtos = await response.Content.ReadFromJsonAsync<List<DiscoveryDeviceDto>>(JsonOptions, cancellationToken);
            return (dtos ?? []).Select(x => x.ToDomain()).ToList();
        }

        /// <inheritdoc />
        public async Task<Device?> GetDeviceAsync(string deviceId, CancellationToken cancellationToken = default)
        {
            HttpResponseMessage response = await _httpClient.GetAsync($"/api/v1/discovery/devices/{deviceId}", cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }

            _ = response.EnsureSuccessStatusCode();
            DiscoveryDeviceDto? dto = await response.Content.ReadFromJsonAsync<DiscoveryDeviceDto>(JsonOptions, cancellationToken);
            return dto?.ToDomain();
        }
    }
}
