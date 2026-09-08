using System.Net.Http.Json;
using System.Text.Json;

using VideoForensics.Api.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Hosting.Remote
{
    /// <summary>
    /// HTTP-backed <see cref="IDeviceConfigRepository"/> that calls the server's Minimal API
    /// (see VideoForensics.WebApp/Api/DeviceConfigEndpoints.cs) for device configuration snapshots.
    /// </summary>
    public class RemoteDeviceConfigRepository : IDeviceConfigRepository
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly HttpClient _httpClient;

        public RemoteDeviceConfigRepository(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <inheritdoc />
        public async Task<DeviceConfigSnapshot?> GetAsync(Guid snapshotId, CancellationToken ct)
        {
            HttpResponseMessage response = await _httpClient.GetAsync($"/api/v1/device-config/{snapshotId}", ct);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }
            DeviceConfigSnapshotDto? dto = await response.Content.ReadFromJsonAsync<DeviceConfigSnapshotDto>(JsonOptions, ct);
            return dto?.ToDomain();
        }

        /// <inheritdoc />
        public async Task<DeviceConfigSnapshot> AppendSnapshotAsync(DeviceConfigSnapshot snapshot, CancellationToken ct)
        {
            var request = new
            {
                DeviceId = snapshot.DeviceId,
                MotionDetectionEnabled = snapshot.MotionDetectionEnabled,
                MotionSensitivity = snapshot.MotionSensitivity,
                RecordingMode = snapshot.RecordingMode,
                CustomSettingsJson = snapshot.CustomSettingsJson,
                CapturedAtUtc = snapshot.CapturedAtUtc,
                Source = snapshot.Source.ToString()
            };

            HttpResponseMessage response = await _httpClient.PostAsJsonAsync("/api/v1/device-config", request, ct);
            _ = response.EnsureSuccessStatusCode();
            DeviceConfigSnapshotDto? dto = await response.Content.ReadFromJsonAsync<DeviceConfigSnapshotDto>(JsonOptions, ct);
            return (dto ?? throw new InvalidOperationException("Server returned null response for device config snapshot")).ToDomain();
        }

        /// <inheritdoc />
        public async Task<DeviceConfigSnapshot?> GetLatestAsync(Guid deviceId, CancellationToken ct)
        {
            HttpResponseMessage response = await _httpClient.GetAsync($"/api/v1/device-config/by-device/{deviceId}", ct);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }
            DeviceConfigSnapshotDto? dto = await response.Content.ReadFromJsonAsync<DeviceConfigSnapshotDto>(JsonOptions, ct);
            return dto?.ToDomain();
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<DeviceConfigSnapshot>> GetHistoryAsync(Guid deviceId, CancellationToken ct)
        {
            HttpResponseMessage response = await _httpClient.GetAsync($"/api/v1/device-config/history/{deviceId}", ct);
            _ = response.EnsureSuccessStatusCode();
            List<DeviceConfigSnapshotDto>? dtos = await response.Content.ReadFromJsonAsync<List<DeviceConfigSnapshotDto>>(JsonOptions, ct);
            return (dtos ?? []).Select(x => x.ToDomain()).ToList();
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<DeviceConfigSnapshot>> ListAsync(CancellationToken ct)
        {
            HttpResponseMessage response = await _httpClient.GetAsync("/api/v1/device-config/", ct);
            _ = response.EnsureSuccessStatusCode();
            List<DeviceConfigSnapshotDto>? dtos = await response.Content.ReadFromJsonAsync<List<DeviceConfigSnapshotDto>>(JsonOptions, ct);
            return (dtos ?? []).Select(x => x.ToDomain()).ToList();
        }
    }
}
