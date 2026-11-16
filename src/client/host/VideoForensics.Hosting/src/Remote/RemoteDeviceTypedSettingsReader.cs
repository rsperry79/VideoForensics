using System.Net.Http.Json;
using System.Text.Json;

using VideoForensics.Core.Api.Contracts;
using VideoForensics.Core.Api.Contracts.Repositories;

namespace VideoForensics.Client.Hosting.Remote
{
    /// <summary>
    /// HTTP-backed <see cref="IDeviceTypedSettingsReader"/> that reads the server's typed settings route
    /// (/api/v1/device-config/typed/{deviceId}) and returns the DTO as-is.
    /// </summary>
    public class RemoteDeviceTypedSettingsReader : IDeviceTypedSettingsReader
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly HttpClient _httpClient;

        public RemoteDeviceTypedSettingsReader(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <inheritdoc />
        public async Task<DeviceTypedSettingsDto> GetAsync(Guid deviceId, CancellationToken ct)
        {
            HttpResponseMessage response = await _httpClient.GetAsync($"/api/v1/device-config/typed/{deviceId}", ct);
            _ = response.EnsureSuccessStatusCode();
            DeviceTypedSettingsDto? dto = await response.Content.ReadFromJsonAsync<DeviceTypedSettingsDto>(JsonOptions, ct);
            return dto ?? new DeviceTypedSettingsDto(deviceId, null, [], []);
        }
    }
}
