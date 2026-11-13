using System.Net.Http.Json;
using System.Text.Json;

using VideoForensics.Core.Api.Contracts;
using VideoForensics.Core.Api.Contracts.Repositories;

namespace VideoForensics.Client.Hosting.Remote
{
    /// <summary>
    /// HTTP-backed <see cref="IDeviceRepository"/> that reads devices from the server's /api/v1/devices endpoint.
    /// Writes are server-only and are not part of this interface.
    /// </summary>
    public class RemoteDeviceRepository : IDeviceRepository
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly HttpClient _httpClient;

        public RemoteDeviceRepository(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<DeviceDto>> ListAsync(CancellationToken ct)
        {
            HttpResponseMessage response = await _httpClient.GetAsync("/api/v1/devices", ct);
            _ = response.EnsureSuccessStatusCode();
            List<DeviceDto>? dtos = await response.Content.ReadFromJsonAsync<List<DeviceDto>>(JsonOptions, ct);
            return dtos ?? [];
        }

        /// <inheritdoc />
        /// <remarks>
        /// No single-device endpoint exists yet, so this filters <see cref="ListAsync"/> client-side.
        /// </remarks>
        public async Task<DeviceDto?> GetAsync(Guid deviceId, CancellationToken ct)
        {
            IReadOnlyList<DeviceDto> devices = await ListAsync(ct);
            return devices.FirstOrDefault(d => d.Id == deviceId);
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<DeviceDto>> GetByLocationIdAsync(Guid locationId, CancellationToken ct)
        {
            IReadOnlyList<DeviceDto> devices = await ListAsync(ct);
            return devices.Where(d => d.LocationId == locationId).ToList();
        }

        /// <inheritdoc />
        public async Task<DeviceDto?> GetByProviderDeviceIdAsync(Guid locationId, string providerDeviceId, CancellationToken ct)
        {
            IReadOnlyList<DeviceDto> devices = await ListAsync(ct);
            return devices.FirstOrDefault(d => d.LocationId == locationId && d.ProviderDeviceId == providerDeviceId);
        }
    }
}
