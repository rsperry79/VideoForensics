using System.Net.Http.Json;
using System.Text.Json;

using VideoForensics.Api.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting.Contracts;

namespace VideoForensics.Hosting.Remote
{
    /// <summary>
    /// HTTP-backed implementation of <see cref="ILockoutPolicyService"/> that calls the server's Minimal API
    /// (see VideoForensics.WebApp/Api/LockoutPolicyEndpoints.cs) instead of accessing the database directly.
    /// The client (MAUI or other thin client) uses this as part of the client/server split.
    /// </summary>
    public sealed class RemoteLockoutPolicyService : ILockoutPolicyService
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly HttpClient _httpClient;

        public RemoteLockoutPolicyService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <inheritdoc />
        public async Task<LockoutPolicySettings> GetAsync(CancellationToken ct)
        {
            HttpResponseMessage response = await _httpClient.GetAsync("/api/v1/lockout-policy", ct);
            _ = response.EnsureSuccessStatusCode();
            LockoutPolicySettingsDto? dto = await response.Content.ReadFromJsonAsync<LockoutPolicySettingsDto>(JsonOptions, ct);
            LockoutPolicySettingsDto result = dto ?? throw new InvalidOperationException("Server returned null lockout policy settings");

            // Convert DTO to domain, setting a default ID (server manages the singleton ID)
            return new LockoutPolicySettings
            {
                Id = Guid.NewGuid(),
                MaxFailedAttempts = result.MaxFailedAttempts,
                LockoutDurationMinutes = result.LockoutDurationMinutes,
                BlockedCountryCodes = result.BlockedCountryCodes,
                FailClosedOnLookupError = result.FailClosedOnLookupError,
                UpdatedAtUtc = result.UpdatedAtUtc,
                UpdatedByOperatorId = null
            };
        }

        /// <inheritdoc />
        public async Task UpdateAsync(LockoutPolicySettings settings, CancellationToken ct)
        {
            var requestDto = new UpdateLockoutPolicySettingsRequest(
                MaxFailedAttempts: settings.MaxFailedAttempts,
                LockoutDurationMinutes: settings.LockoutDurationMinutes,
                BlockedCountryCodes: settings.BlockedCountryCodes,
                FailClosedOnLookupError: settings.FailClosedOnLookupError
            );

            HttpResponseMessage response = await _httpClient.PutAsJsonAsync("/api/v1/lockout-policy", requestDto, cancellationToken: ct);
            _ = response.EnsureSuccessStatusCode();
        }
    }
}
