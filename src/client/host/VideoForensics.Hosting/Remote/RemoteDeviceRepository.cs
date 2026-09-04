using System.Net.Http.Json;
using System.Text.Json;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Hosting.Remote
{
    /// <summary>
    /// HTTP-backed, read-only <see cref="IDeviceRepository"/> that calls the server's Minimal API
    /// (see VideoForensics.WebApp/Api/MediaApiEndpoints.cs) instead of a local database - the MAUI
    /// client's implementation of the "thin client talks to a server API" half of the plan's client/
    /// server split (§4/M5). Only <see cref="ListAsync"/> and <see cref="GetAsync"/> (derived
    /// client-side from it, since there's no single-device endpoint yet) are backed by a real
    /// endpoint; every write and every other read throws <see cref="NotSupportedException"/>.
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
        public async Task<IReadOnlyList<Device>> ListAsync(CancellationToken ct)
        {
            var response = await _httpClient.GetAsync("/api/devices", ct);
            response.EnsureSuccessStatusCode();
            var devices = await response.Content.ReadFromJsonAsync<List<Device>>(JsonOptions, ct);
            return devices ?? [];
        }

        /// <inheritdoc />
        /// <remarks>
        /// No dedicated single-device server endpoint exists yet, so this filters the full
        /// <see cref="ListAsync"/> result client-side rather than inventing a new endpoint.
        /// </remarks>
        public async Task<Device?> GetAsync(Guid deviceId, CancellationToken ct)
        {
            var devices = await ListAsync(ct);
            return devices.FirstOrDefault(d => d.Id == deviceId);
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<Device>> GetByLocationIdAsync(Guid locationId, CancellationToken ct) =>
            throw new NotSupportedException("Not supported on a remote (MAUI client) repository - use the server's API directly, or this read isn't wired up yet.");

        /// <inheritdoc />
        public Task<Device?> GetByProviderDeviceIdAsync(Guid locationId, string providerDeviceId, CancellationToken ct) =>
            throw new NotSupportedException("Not supported on a remote (MAUI client) repository - use the server's API directly, or this read isn't wired up yet.");

        /// <inheritdoc />
        public Task AddAsync(Device device, CancellationToken ct) =>
            throw new NotSupportedException("Not supported on a remote (MAUI client) repository - use the server's API directly, or this read isn't wired up yet.");

        /// <inheritdoc />
        public Task UpdateAsync(Device device, CancellationToken ct) =>
            throw new NotSupportedException("Not supported on a remote (MAUI client) repository - use the server's API directly, or this read isn't wired up yet.");

        /// <inheritdoc />
        public Task UpdateLastSuccessfulPullAsync(Guid deviceId, DateTime pulledAtUtc, CancellationToken ct) =>
            throw new NotSupportedException("Not supported on a remote (MAUI client) repository - use the server's API directly, or this read isn't wired up yet.");

        /// <inheritdoc />
        public Task DeleteAsync(Guid deviceId, CancellationToken ct) =>
            throw new NotSupportedException("Not supported on a remote (MAUI client) repository - use the server's API directly, or this read isn't wired up yet.");
    }
}
