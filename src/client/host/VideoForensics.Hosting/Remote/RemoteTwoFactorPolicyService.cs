using System.Net.Http.Json;
using System.Text.Json;

using VideoForensics.Api.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting.Contracts;

namespace VideoForensics.Hosting.Remote
{
    /// <summary>
    /// HTTP-backed implementation of <see cref="ITwoFactorPolicyService"/> that calls the server's Minimal API
    /// (see VideoForensics.WebApp/Api/TwoFactorPolicyEndpoints.cs) instead of accessing the database directly.
    /// The client (MAUI or other thin client) uses this as part of the client/server split.
    /// </summary>
    public sealed class RemoteTwoFactorPolicyService : ITwoFactorPolicyService
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly HttpClient _httpClient;

        public RemoteTwoFactorPolicyService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<TwoFactorRoleRequirement>> GetRoleRequirementsAsync(CancellationToken ct)
        {
            HttpResponseMessage response = await _httpClient.GetAsync("/api/v1/two-factor-policy/roles", ct);
            _ = response.EnsureSuccessStatusCode();
            List<TwoFactorRoleRequirementDto>? dtos = await response.Content.ReadFromJsonAsync<List<TwoFactorRoleRequirementDto>>(JsonOptions, ct);
            List<TwoFactorRoleRequirementDto> result = dtos ?? throw new InvalidOperationException("Server returned null two-factor role requirements");

            return result.Select(dto => new TwoFactorRoleRequirement
            {
                Id = Guid.NewGuid(),
                Role = dto.Role,
                RequireTwoFactor = dto.RequireTwoFactor,
                UpdatedAtUtc = dto.UpdatedAtUtc,
                UpdatedByOperatorId = null
            }).ToList();
        }

        /// <inheritdoc />
        public async Task UpdateRoleRequirementAsync(OperatorRole role, bool requireTwoFactor, CancellationToken ct)
        {
            var request = new UpdateTwoFactorRoleRequirementRequest(requireTwoFactor);
            HttpResponseMessage response = await _httpClient.PutAsJsonAsync($"/api/v1/two-factor-policy/roles/{role}", request, cancellationToken: ct);
            _ = response.EnsureSuccessStatusCode();
        }

        /// <inheritdoc />
        public async Task UpdateOperatorOverrideAsync(Guid operatorId, TwoFactorRequirementOverride @override, CancellationToken ct)
        {
            var request = new UpdateOperatorTwoFactorOverrideRequest(@override);
            HttpResponseMessage response = await _httpClient.PutAsJsonAsync($"/api/v1/two-factor-policy/operators/{operatorId}/override", request, cancellationToken: ct);
            _ = response.EnsureSuccessStatusCode();
        }
    }
}
