using System.Net.Http.Json;
using System.Text.Json;

using VideoForensics.Api.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Hosting.Remote
{
    /// <summary>
    /// HTTP-backed <see cref="IProviderAccountRepository"/> that calls the server's Minimal API
    /// (see VideoForensics.WebApp/Api/AccountEndpoints.cs) instead of a local database.
    /// Part of the "thin client talks to a server API" implementation of the client/server split.
    /// </summary>
    public class RemoteProviderAccountRepository : IProviderAccountRepository
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly HttpClient _httpClient;

        public RemoteProviderAccountRepository(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <inheritdoc />
        public async Task<ProviderAccount?> GetAsync(Guid accountId, CancellationToken ct)
        {
            HttpResponseMessage response = await _httpClient.GetAsync($"/api/v1/accounts/provider-accounts/{accountId}", ct);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }

            _ = response.EnsureSuccessStatusCode();
            ProviderAccountDto? dto = await response.Content.ReadFromJsonAsync<ProviderAccountDto>(JsonOptions, ct);
            return dto?.ToDomain();
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<ProviderAccount>> GetByUserIdAsync(Guid userId, CancellationToken ct)
        {
            HttpResponseMessage response = await _httpClient.GetAsync($"/api/v1/accounts/provider-accounts/by-user/{userId}", ct);
            _ = response.EnsureSuccessStatusCode();
            List<ProviderAccountDto>? dtos = await response.Content.ReadFromJsonAsync<List<ProviderAccountDto>>(JsonOptions, ct);
            return (dtos ?? []).Select(x => x.ToDomain()).ToList();
        }

        /// <inheritdoc />
        public async Task<ProviderAccount?> GetByUserAndProviderAsync(Guid userId, string providerName, CancellationToken ct)
        {
            HttpResponseMessage response = await _httpClient.GetAsync($"/api/v1/accounts/provider-accounts/by-user-and-provider?userId={userId}&providerName={Uri.EscapeDataString(providerName)}", ct);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }

            _ = response.EnsureSuccessStatusCode();
            ProviderAccountDto? dto = await response.Content.ReadFromJsonAsync<ProviderAccountDto>(JsonOptions, ct);
            return dto?.ToDomain();
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<ProviderAccount>> ListAsync(CancellationToken ct)
        {
            HttpResponseMessage response = await _httpClient.GetAsync("/api/v1/accounts/provider-accounts", ct);
            _ = response.EnsureSuccessStatusCode();
            List<ProviderAccountDto>? dtos = await response.Content.ReadFromJsonAsync<List<ProviderAccountDto>>(JsonOptions, ct);
            return (dtos ?? []).Select(x => x.ToDomain()).ToList();
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<ProviderAccount>> ListActiveAsync(CancellationToken ct)
        {
            HttpResponseMessage response = await _httpClient.GetAsync("/api/v1/accounts/provider-accounts/active", ct);
            _ = response.EnsureSuccessStatusCode();
            List<ProviderAccountDto>? dtos = await response.Content.ReadFromJsonAsync<List<ProviderAccountDto>>(JsonOptions, ct);
            return (dtos ?? []).Select(x => x.ToDomain()).ToList();
        }

        /// <inheritdoc />
        public async Task AddAsync(ProviderAccount account, CancellationToken ct)
        {
            var request = new
            {
                account.UserId,
                account.ProviderName
            };

            HttpResponseMessage response = await _httpClient.PostAsJsonAsync("/api/v1/accounts/provider-accounts", request, ct);
            _ = response.EnsureSuccessStatusCode();
        }

        /// <inheritdoc />
        public async Task UpdateAsync(ProviderAccount account, CancellationToken ct)
        {
            var request = new
            {
                account.IsActive,
                account.LastSuccessfulAuthUtc,
                account.LastDownloadTimeUtc
            };

            HttpResponseMessage response = await _httpClient.PutAsJsonAsync($"/api/v1/accounts/provider-accounts/{account.Id}", request, ct);
            _ = response.EnsureSuccessStatusCode();
        }

        /// <inheritdoc />
        public async Task DeleteAsync(Guid accountId, CancellationToken ct)
        {
            HttpResponseMessage response = await _httpClient.DeleteAsync($"/api/v1/accounts/provider-accounts/{accountId}", ct);
            _ = response.EnsureSuccessStatusCode();
        }
    }
}
