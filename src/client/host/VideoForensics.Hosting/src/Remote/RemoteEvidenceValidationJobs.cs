using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using VideoForensics.Core.Api.Contracts;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Hosting.Remote
{
    /// <summary>
    /// HTTP-backed implementation of <see cref="IEvidenceValidationJobs"/> that starts and polls validation jobs on the
    /// server's Minimal API (see VideoForensics.WebApp/Api/EvidenceEndpoints.cs). Part of the client/server split.
    /// </summary>
    public class RemoteEvidenceValidationJobs : IEvidenceValidationJobs
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly HttpClient _httpClient;

        public RemoteEvidenceValidationJobs(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <inheritdoc />
        public Task<EvidenceValidationJob> StartIntegrityAsync(Guid? deviceId, CancellationToken ct) =>
            PostIntegrityAsync(new StartIntegrityJobRequest(deviceId), ct);

        /// <inheritdoc />
        public Task<EvidenceValidationJob> StartIntegrityForDevicesAsync(IReadOnlyList<Guid> deviceIds, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(deviceIds);
            return PostIntegrityAsync(new StartIntegrityJobRequest(null, deviceIds.ToArray()), ct);
        }

        private async Task<EvidenceValidationJob> PostIntegrityAsync(StartIntegrityJobRequest request, CancellationToken ct)
        {
            string jsonContent = JsonSerializer.Serialize(request);
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/evidence/jobs/integrity")
            {
                Content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json")
            };

            HttpResponseMessage response = await _httpClient.SendAsync(httpRequest, ct);
            _ = response.EnsureSuccessStatusCode();

            EvidenceValidationJobDto? dto = await response.Content.ReadFromJsonAsync<EvidenceValidationJobDto>(JsonOptions, ct);
            return (dto ?? throw new InvalidOperationException("Server returned an empty evidence validation job.")).ToDomain();
        }

        /// <inheritdoc />
        public async Task<EvidenceValidationJob> StartReconcileAsync(
            Guid deviceId,
            string providerDeviceId,
            DateTime fromUtc,
            DateTime toUtc,
            CancellationToken ct)
        {
            var request = new StartReconcileJobRequest(deviceId, providerDeviceId, fromUtc, toUtc);
            string jsonContent = JsonSerializer.Serialize(request);
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/evidence/jobs/reconcile")
            {
                Content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json")
            };

            HttpResponseMessage response = await _httpClient.SendAsync(httpRequest, ct);
            _ = response.EnsureSuccessStatusCode();

            EvidenceValidationJobDto? dto = await response.Content.ReadFromJsonAsync<EvidenceValidationJobDto>(JsonOptions, ct);
            return (dto ?? throw new InvalidOperationException("Server returned an empty evidence validation job.")).ToDomain();
        }

        /// <inheritdoc />
        public async Task<EvidenceValidationJob?> GetLatestAsync(EvidenceValidationJobKind kind, CancellationToken ct)
        {
            string kindSegment = kind == EvidenceValidationJobKind.Integrity ? "integrity" : "reconcile";
            using var httpRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/evidence/jobs/{kindSegment}/latest");

            HttpResponseMessage response = await _httpClient.SendAsync(httpRequest, ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            _ = response.EnsureSuccessStatusCode();

            EvidenceValidationJobDto? dto = await response.Content.ReadFromJsonAsync<EvidenceValidationJobDto>(JsonOptions, ct);
            return dto?.ToDomain();
        }
    }

    /// <summary>Maps evidence validation job DTOs from the server to the client-side domain object.</summary>
    internal static class EvidenceValidationJobDtoMapping
    {
        /// <summary>Converts a job DTO to an <see cref="EvidenceValidationJob"/>, parsing Kind and State from their string forms.</summary>
        public static EvidenceValidationJob ToDomain(this EvidenceValidationJobDto dto)
        {
            return new EvidenceValidationJob
            {
                Id = dto.Id,
                Kind = Enum.Parse<EvidenceValidationJobKind>(dto.Kind, ignoreCase: true),
                State = Enum.Parse<EvidenceValidationJobState>(dto.State, ignoreCase: true),
                StartedUtc = dto.StartedUtc,
                CompletedUtc = dto.CompletedUtc,
                Log = dto.Log,
                IntegrityResults = dto.IntegrityResults?.Select(r => new MediaVerificationResult
                {
                    MediaItemId = r.MediaItemId,
                    FileName = r.FileName,
                    Status = r.Status,
                    FailureReason = r.FailureReason,
                    DeviceId = r.DeviceId
                }).ToList(),
                Discrepancies = dto.Discrepancies?.Select(d => d.ToDomain()).ToList(),
                ErrorMessage = dto.ErrorMessage
            };
        }
    }
}
