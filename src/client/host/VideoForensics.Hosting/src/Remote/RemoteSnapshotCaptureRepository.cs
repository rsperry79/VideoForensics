using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.Hosting.Remote
{
    /// <summary>
    /// HTTP-backed <see cref="ISnapshotCaptureRepository"/> that calls the server's <c>/api/v1/snapshot-captures</c> route
    /// (see VideoForensics.WebApp/Api/SnapshotCaptureEndpoints.cs), so a MAUI client lists snapshots in a flag window without a local database.
    /// <see cref="AddAsync"/> is server-only: captures are taken by the server's snapshot service.
    /// </summary>
    public sealed class RemoteSnapshotCaptureRepository : ISnapshotCaptureRepository
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private const string ServerOnlyMessage = "Snapshot captures are taken and recorded by the server's snapshot service; a remote client cannot write them.";

        private readonly HttpClient _httpClient;

        public RemoteSnapshotCaptureRepository(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <inheritdoc />
        public Task<SnapshotCapture> AddAsync(SnapshotCapture capture, CancellationToken ct)
        {
            throw new NotSupportedException(ServerOnlyMessage);
        }

        /// <inheritdoc />
        public async Task<SnapshotCapture?> GetAsync(Guid id, CancellationToken ct)
        {
            HttpResponseMessage response = await _httpClient.GetAsync($"/api/v1/snapshot-captures/{id:D}", ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            _ = response.EnsureSuccessStatusCode();
            SnapshotCaptureDto? dto = await response.Content.ReadFromJsonAsync<SnapshotCaptureDto>(JsonOptions, ct);
            return dto == null ? null : ToDomain(dto);
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<SnapshotCapture>> GetCurrentForDeviceAsync(Guid deviceId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
        {
            return ListAsync(deviceId, from, to, attemptsOnly: false, ct);
        }

        /// <inheritdoc />
        public Task<IReadOnlyList<SnapshotCapture>> GetAttemptsForDeviceAsync(Guid deviceId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
        {
            return ListAsync(deviceId, from, to, attemptsOnly: true, ct);
        }

        private async Task<IReadOnlyList<SnapshotCapture>> ListAsync(Guid deviceId, DateTimeOffset from, DateTimeOffset to, bool attemptsOnly, CancellationToken ct)
        {
            string query = $"deviceId={deviceId:D}&from={Uri.EscapeDataString(from.ToString("O"))}"
                + $"&to={Uri.EscapeDataString(to.ToString("O"))}&attemptsOnly={(attemptsOnly ? "true" : "false")}";
            HttpResponseMessage response = await _httpClient.GetAsync("/api/v1/snapshot-captures?" + query, ct);
            _ = response.EnsureSuccessStatusCode();
            List<SnapshotCaptureDto>? dtos = await response.Content.ReadFromJsonAsync<List<SnapshotCaptureDto>>(JsonOptions, ct);
            return (dtos ?? []).Select(ToDomain).ToList();
        }

        /// <summary>Maps one wire row back to the domain type, keeping the server's id and stored fields.</summary>
        private static SnapshotCapture ToDomain(SnapshotCaptureDto dto)
        {
            return SnapshotCapture.Rehydrate(
                dto.Id, dto.DeviceId, dto.CapturedAtUtc, dto.Trigger, dto.HttpStatus, dto.IsCurrent, dto.MediaFilePath, dto.DeviceHealthId);
        }
    }
}
