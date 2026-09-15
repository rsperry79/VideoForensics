using System.Net.Http.Json;
using System.Text.Json;

using VideoForensics.Api.Contracts;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Hosting.Remote
{
    /// <summary>
    /// HTTP-backed implementation of <see cref="IEvidenceValidationService"/> that calls the server's Minimal API
    /// (see VideoForensics.WebApp/Api/EvidenceEndpoints.cs) instead of performing validation locally.
    /// The MAUI client uses this as part of the client/server split (§4/M5).
    /// </summary>
    public class RemoteEvidenceValidationService : IEvidenceValidationService
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly HttpClient _httpClient;

        public RemoteEvidenceValidationService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<MediaVerificationResult>> VerifyLocalIntegrityAsync(Guid? deviceId, CancellationToken ct)
        {
            var request = new VerifyLocalIntegrityRequest(deviceId);
            string jsonContent = JsonSerializer.Serialize(request);
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/evidence/verify-local-integrity")
            {
                Content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json")
            };

            HttpResponseMessage response = await _httpClient.SendAsync(httpRequest, ct);
            _ = response.EnsureSuccessStatusCode();

            List<MediaVerificationResultDto>? dtos = await response.Content.ReadFromJsonAsync<List<MediaVerificationResultDto>>(JsonOptions, ct);
            return (dtos ?? []).Select(dto => new MediaVerificationResult
            {
                MediaItemId = dto.MediaItemId,
                FileName = dto.FileName,
                Status = dto.Status,
                FailureReason = dto.FailureReason
            }).ToList();
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<ReconciliationDiscrepancy>> ReconcileWithProviderAsync(
            Guid deviceId,
            string providerDeviceId,
            DateTime fromUtc,
            DateTime toUtc,
            CancellationToken ct)
        {
            var request = new ReconcileWithProviderRequest(deviceId, providerDeviceId, fromUtc, toUtc);
            string jsonContent = JsonSerializer.Serialize(request);
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/evidence/reconcile")
            {
                Content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json")
            };

            HttpResponseMessage response = await _httpClient.SendAsync(httpRequest, ct);
            _ = response.EnsureSuccessStatusCode();

            List<ReconciliationDiscrepancyDto>? dtos = await response.Content.ReadFromJsonAsync<List<ReconciliationDiscrepancyDto>>(JsonOptions, ct);
            return (dtos ?? []).Select(d => d.ToDomain()).ToList();
        }
    }

    /// <summary>Request to verify local integrity of downloaded media files.</summary>
    /// <param name="DeviceId">Device to verify, or null to verify all.</param>
    internal record VerifyLocalIntegrityRequest(Guid? DeviceId);

    /// <summary>Request to reconcile stored events with provider's current records.</summary>
    /// <param name="DeviceId">Database device ID.</param>
    /// <param name="ProviderDeviceId">Provider's device identifier.</param>
    /// <param name="FromUtc">Start of reconciliation date range.</param>
    /// <param name="ToUtc">End of reconciliation date range.</param>
    internal record ReconcileWithProviderRequest(
        Guid DeviceId,
        string ProviderDeviceId,
        DateTime FromUtc,
        DateTime ToUtc
    );
}
