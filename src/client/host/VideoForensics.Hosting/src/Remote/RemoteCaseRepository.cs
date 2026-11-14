using System.Net.Http.Json;
using System.Text.Json;

using VideoForensics.Core.Api.Contracts;
using VideoForensics.Core.Api.Contracts.Repositories;

namespace VideoForensics.Client.Hosting.Remote
{
    /// <summary>
    /// HTTP-backed <see cref="ICaseRepository"/> that calls the server's Minimal API
    /// (see VideoForensics.WebApp/Api/CaseEndpoints.cs) instead of a local database - the client's
    /// implementation of the "thin client talks to a server API" half of the client/server split.
    ///
    /// All methods delegate to the server's case endpoints, which enforce role-based authorization
    /// for write operations. Responses are deserialized straight into DTOs; no entity conversion happens here.
    ///
    /// Note: The actor string parameters (createdBy, updatedBy, etc.) are ignored client-side;
    /// the server takes the actor from the paired-device bearer token. These parameters are kept
    /// for API compatibility with the ICaseRepository interface.
    /// </summary>
    public class RemoteCaseRepository : ICaseRepository
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly HttpClient _httpClient;

        public RemoteCaseRepository(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <inheritdoc />
        public async Task<ForensicCaseDto> CreateAsync(
            string? caseNumber,
            string title,
            string? description,
            Guid? leadOperatorId,
            DateTime? scopeFromUtc,
            DateTime? scopeToUtc,
            IReadOnlyCollection<Guid> deviceIds,
            string createdBy,
            CancellationToken ct)
        {
            var request = new CreateCaseRequestDto(caseNumber, title, description, leadOperatorId, scopeFromUtc, scopeToUtc, deviceIds);
            HttpResponseMessage response = await _httpClient.PostAsJsonAsync("/api/v1/cases", request, cancellationToken: ct);
            _ = response.EnsureSuccessStatusCode();
            ForensicCaseDto? dto = await response.Content.ReadFromJsonAsync<ForensicCaseDto>(JsonOptions, ct);
            return dto ?? throw new InvalidOperationException("Server returned null response");
        }

        /// <inheritdoc />
        public async Task<ForensicCaseDto> CreateFromJammingDetectionAsync(
            JammingIncidentDto jammingIncident,
            CancellationToken ct)
        {
            HttpResponseMessage response = await _httpClient.PostAsJsonAsync("/api/v1/cases/from-jamming-detection", jammingIncident, cancellationToken: ct);
            _ = response.EnsureSuccessStatusCode();
            ForensicCaseDto? dto = await response.Content.ReadFromJsonAsync<ForensicCaseDto>(JsonOptions, ct);
            return dto ?? throw new InvalidOperationException("Server returned null response");
        }

        /// <inheritdoc />
        public async Task<ForensicCaseDto?> GetAsync(Guid id, CancellationToken ct)
        {
            HttpResponseMessage response = await _httpClient.GetAsync($"/api/v1/cases/{id}", ct);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }

            _ = response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<ForensicCaseDto>(JsonOptions, ct);
        }

        /// <inheritdoc />
        public async Task<ForensicCaseDto?> GetByNumberAsync(string caseNumber, CancellationToken ct)
        {
            HttpResponseMessage response = await _httpClient.GetAsync($"/api/v1/cases/by-number/{Uri.EscapeDataString(caseNumber)}", ct);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }

            _ = response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<ForensicCaseDto>(JsonOptions, ct);
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<ForensicCaseDto>> ListAsync(CaseStatusDto? status, CancellationToken ct)
        {
            string url = "/api/v1/cases";
            if (status.HasValue)
            {
                url += $"?status={status.Value}";
            }

            HttpResponseMessage response = await _httpClient.GetAsync(url, ct);
            _ = response.EnsureSuccessStatusCode();
            List<ForensicCaseDto>? dtos = await response.Content.ReadFromJsonAsync<List<ForensicCaseDto>>(JsonOptions, ct);
            return dtos ?? [];
        }

        /// <summary>
        /// Calls the account-scoped case route. The server returns <see cref="AccountCaseDto"/> rows, which carry only
        /// the list-view fields, so each returned <see cref="ForensicCaseDto"/> leaves CreatedBy empty, UpdatedAtUtc equal to CreatedAtUtc,
        /// and Description, LeadOperatorId, ScopeToUtc, DeviceIds, and ClosedBy/ClosedAtUtc unset. Callers needing full details must call GetAsync.
        /// </summary>
        /// <inheritdoc />
        public async Task<IReadOnlyList<ForensicCaseDto>> ListForProviderAccountAsync(Guid providerAccountId, CancellationToken ct)
        {
            HttpResponseMessage response = await _httpClient.GetAsync($"/api/v1/accounts/provider-accounts/{providerAccountId}/cases", ct);
            _ = response.EnsureSuccessStatusCode();
            List<AccountCaseDto>? dtos = await response.Content.ReadFromJsonAsync<List<AccountCaseDto>>(JsonOptions, ct);
            return (dtos ?? []).Select(x => new ForensicCaseDto(
                Id: x.Id,
                CaseNumber: x.CaseNumber,
                Title: x.Title,
                Description: null,
                Status: x.Status,
                LeadOperatorId: null,
                CreatedBy: string.Empty,
                CreatedAtUtc: x.CreatedAtUtc,
                UpdatedAtUtc: x.CreatedAtUtc,
                ClosedBy: null,
                ClosedAtUtc: null,
                ScopeFromUtc: x.ScopeFromUtc,
                ScopeToUtc: null,
                DeviceIds: Array.Empty<Guid>())).ToList();
        }

        /// <inheritdoc />
        public async Task UpdateDetailsAsync(
            Guid id,
            string title,
            string? description,
            Guid? leadOperatorId,
            string updatedBy,
            CancellationToken ct)
        {
            var request = new UpdateCaseDetailsRequestDto(title, description, leadOperatorId);
            HttpResponseMessage response = await _httpClient.PutAsJsonAsync($"/api/v1/cases/{id}", request, cancellationToken: ct);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                throw new KeyNotFoundException("Case was not found.");
            }

            if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                string message = await response.Content.ReadAsStringAsync(ct);
                throw new InvalidOperationException(message);
            }

            _ = response.EnsureSuccessStatusCode();
        }

        /// <inheritdoc />
        public async Task SetScopeAsync(
            Guid id,
            DateTime? fromUtc,
            DateTime? toUtc,
            IReadOnlyCollection<Guid> deviceIds,
            string updatedBy,
            CancellationToken ct)
        {
            var request = new SetCaseScopeRequestDto(fromUtc, toUtc, deviceIds);
            HttpResponseMessage response = await _httpClient.PutAsJsonAsync($"/api/v1/cases/{id}/scope", request, cancellationToken: ct);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                throw new KeyNotFoundException("Case was not found.");
            }

            if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                string message = await response.Content.ReadAsStringAsync(ct);
                throw new InvalidOperationException(message);
            }

            _ = response.EnsureSuccessStatusCode();
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<Guid>> GetDeviceIdsAsync(Guid caseId, CancellationToken ct)
        {
            HttpResponseMessage response = await _httpClient.GetAsync($"/api/v1/cases/{caseId}/devices", ct);
            _ = response.EnsureSuccessStatusCode();
            List<Guid>? ids = await response.Content.ReadFromJsonAsync<List<Guid>>(JsonOptions, ct);
            return ids ?? [];
        }

        /// <inheritdoc />
        public async Task CloseAsync(Guid id, string closedBy, CancellationToken ct)
        {
            HttpResponseMessage response = await _httpClient.PostAsync($"/api/v1/cases/{id}/close", null, ct);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                throw new KeyNotFoundException("Case was not found.");
            }

            if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                string message = await response.Content.ReadAsStringAsync(ct);
                throw new InvalidOperationException(message);
            }

            _ = response.EnsureSuccessStatusCode();
        }

        /// <inheritdoc />
        public async Task ReopenAsync(Guid id, string reopenedBy, CancellationToken ct)
        {
            HttpResponseMessage response = await _httpClient.PostAsync($"/api/v1/cases/{id}/reopen", null, ct);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                throw new KeyNotFoundException("Case was not found.");
            }

            if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                string message = await response.Content.ReadAsStringAsync(ct);
                throw new InvalidOperationException(message);
            }

            _ = response.EnsureSuccessStatusCode();
        }

        /// <inheritdoc />
        public async Task<CaseItemDto> AddItemAsync(
            Guid caseId,
            CaseItemKindDto kind,
            Guid targetId,
            string reason,
            string addedBy,
            CancellationToken ct)
        {
            var request = new AddCaseItemRequestDto(kind.ToString(), targetId, reason);
            HttpResponseMessage response = await _httpClient.PostAsJsonAsync($"/api/v1/cases/{caseId}/items", request, cancellationToken: ct);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                throw new KeyNotFoundException("Case or media item was not found.");
            }

            if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                string message = await response.Content.ReadAsStringAsync(ct);
                throw new InvalidOperationException(message);
            }

            _ = response.EnsureSuccessStatusCode();
            CaseItemDto? dto = await response.Content.ReadFromJsonAsync<CaseItemDto>(JsonOptions, ct);
            return dto ?? throw new InvalidOperationException("Server returned null response");
        }

        /// <inheritdoc />
        public async Task RemoveItemAsync(Guid caseItemId, string removedBy, string reason, CancellationToken ct)
        {
            var request = new RemoveCaseItemRequestDto(reason);
            HttpResponseMessage response = await _httpClient.PostAsJsonAsync($"/api/v1/cases/items/{caseItemId}/remove", request, cancellationToken: ct);

            if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
            {
                string message = await response.Content.ReadAsStringAsync(ct);
                throw new InvalidOperationException(message);
            }

            _ = response.EnsureSuccessStatusCode();
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<CaseItemDto>> ListItemsAsync(Guid caseId, bool includeRemoved, CancellationToken ct)
        {
            string url = $"/api/v1/cases/{caseId}/items?includeRemoved={includeRemoved}";
            HttpResponseMessage response = await _httpClient.GetAsync(url, ct);
            _ = response.EnsureSuccessStatusCode();
            List<CaseItemDto>? dtos = await response.Content.ReadFromJsonAsync<List<CaseItemDto>>(JsonOptions, ct);
            return dtos ?? [];
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<ForensicCaseDto>> ListCasesContainingAsync(CaseItemKindDto kind, Guid targetId, CancellationToken ct)
        {
            string url = $"/api/v1/cases/containing?kind={kind}&targetId={targetId}";
            HttpResponseMessage response = await _httpClient.GetAsync(url, ct);
            _ = response.EnsureSuccessStatusCode();
            List<ForensicCaseDto>? dtos = await response.Content.ReadFromJsonAsync<List<ForensicCaseDto>>(JsonOptions, ct);
            return dtos ?? [];
        }
    }
}
