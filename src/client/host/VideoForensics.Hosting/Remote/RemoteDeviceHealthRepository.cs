using System.Net.Http.Json;
using System.Text.Json;

using VideoForensics.Api.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Hosting.Remote
{
    /// <summary>
    /// HTTP-backed, read-only <see cref="IDeviceHealthRepository"/> that calls the server's Minimal
    /// API (see VideoForensics.WebApp/Api/MediaApiEndpoints.cs's device-health route) instead of a
    /// local database - the MAUI client's implementation of the "thin client talks to a server API"
    /// half of the plan's client/server split. Only the date-range history read used by the Evidence
    /// "Device x Time" view's RSSI plot is backed; every other read/write throws
    /// <see cref="NotSupportedException"/> because MAUI has no write path to server-owned health data
    /// and no endpoint for "full history" or "latest only" exists yet.
    /// </summary>
    public class RemoteDeviceHealthRepository : IDeviceHealthRepository
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private const string NotSupportedMessage =
            "Not supported on a remote (MAUI client) repository - use the server's API directly, or this read isn't wired up yet.";

        private const string NoWritePathMessage =
            "MAUI has no write path to server-owned device health data - new health metrics only ever originate server-side, from a provider sync the server itself executed.";

        private readonly HttpClient _httpClient;

        public RemoteDeviceHealthRepository(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<DeviceHealth>> GetHistoryAsync(Guid deviceId, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
        {
            string fromEscaped = Uri.EscapeDataString(fromUtc.ToString("O"));
            string toEscaped = Uri.EscapeDataString(toUtc.ToString("O"));
            HttpResponseMessage response = await _httpClient.GetAsync($"/api/v1/devices/{deviceId}/health?from={fromEscaped}&to={toEscaped}", ct);
            _ = response.EnsureSuccessStatusCode();
            List<DeviceHealthDto>? dtos = await response.Content.ReadFromJsonAsync<List<DeviceHealthDto>>(JsonOptions, ct);
            return (dtos ?? []).Select(x => x.ToDomain()).ToList();
        }

        /// <inheritdoc />
        public Task<DeviceHealth> AddAsync(DeviceHealth health, CancellationToken ct)
        {
            throw new NotSupportedException(NoWritePathMessage);
        }

        /// <inheritdoc />
        public Task<DeviceHealth?> GetLatestAsync(Guid deviceId, CancellationToken ct)
        {
            throw new NotSupportedException(NotSupportedMessage);
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<DeviceHealth>> GetHistoryAsync(Guid deviceId, CancellationToken ct)
        {
            throw new NotSupportedException(NotSupportedMessage);
        }
    }
}
