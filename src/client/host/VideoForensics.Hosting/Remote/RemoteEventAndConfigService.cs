using System.Net.Http.Json;
using System.Text.Json;

using VideoForensics.Api.Contracts;
using VideoForensics.Providers.Common.Contracts;

namespace VideoForensics.Hosting.Remote
{
    /// <summary>
    /// HTTP-backed <see cref="IEventAndConfigService"/> that calls the server's Minimal API
    /// (see VideoForensics.WebApp/Api/DiscoveryEndpoints.cs) instead of using a provider SDK directly.
    /// This is the client-side implementation of the "thin client talks to a server API" half of the plan's
    /// client/server split (§4/M5).
    /// </summary>
    public class RemoteEventAndConfigService : IEventAndConfigService
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly HttpClient _httpClient;

        public RemoteEventAndConfigService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<DeviceEvent>> GetEventsAsync(
            string deviceId,
            DateTime startDate,
            DateTime endDate,
            string? eventType = null,
            CancellationToken cancellationToken = default)
        {
            var queryParams = new List<string>
            {
                $"startDate={startDate:O}",
                $"endDate={endDate:O}"
            };

            if (!string.IsNullOrEmpty(eventType))
            {
                queryParams.Add($"eventType={Uri.EscapeDataString(eventType)}");
            }

            string queryString = string.Join("&", queryParams);
            string url = $"/api/v1/discovery/devices/{deviceId}/events?{queryString}";

            HttpResponseMessage response = await _httpClient.GetAsync(url, cancellationToken);
            _ = response.EnsureSuccessStatusCode();
            List<DeviceEventDto>? dtos = await response.Content.ReadFromJsonAsync<List<DeviceEventDto>>(JsonOptions, cancellationToken);
            return (dtos ?? []).Select(x => x.ToDomain()).ToList();
        }

        /// <inheritdoc />
        public async Task<DeviceConfig?> GetDeviceConfigAsync(string deviceId, CancellationToken cancellationToken = default)
        {
            HttpResponseMessage response = await _httpClient.GetAsync($"/api/v1/discovery/devices/{deviceId}/config", cancellationToken);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }

            _ = response.EnsureSuccessStatusCode();
            DeviceConfigDto? dto = await response.Content.ReadFromJsonAsync<DeviceConfigDto>(JsonOptions, cancellationToken);
            return dto?.ToDomain();
        }

        /// <inheritdoc />
        public async Task<bool> UpdateDeviceConfigAsync(string deviceId, DeviceConfig config, CancellationToken cancellationToken = default)
        {
            DeviceConfigDto configDto = config.ToDto();
            HttpResponseMessage response = await _httpClient.PutAsJsonAsync(
                $"/api/v1/discovery/devices/{deviceId}/config",
                configDto,
                cancellationToken
            );

            return response.IsSuccessStatusCode;
        }
    }
}
