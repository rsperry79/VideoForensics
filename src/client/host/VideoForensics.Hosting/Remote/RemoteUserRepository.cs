using System.Net.Http.Json;
using System.Text.Json;

using VideoForensics.Api.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Hosting.Remote
{
    /// <summary>
    /// HTTP-backed <see cref="IUserRepository"/> that calls the server's Minimal API
    /// (see VideoForensics.WebApp/Api/AccountEndpoints.cs) instead of a local database.
    /// Part of the "thin client talks to a server API" implementation of the client/server split.
    /// </summary>
    public class RemoteUserRepository : IUserRepository
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly HttpClient _httpClient;

        public RemoteUserRepository(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <inheritdoc />
        public async Task<User?> GetAsync(Guid userId, CancellationToken ct)
        {
            HttpResponseMessage response = await _httpClient.GetAsync($"/api/v1/accounts/users/{userId}", ct);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }

            _ = response.EnsureSuccessStatusCode();
            UserDto? dto = await response.Content.ReadFromJsonAsync<UserDto>(JsonOptions, ct);
            return dto?.ToDomain();
        }

        /// <inheritdoc />
        public async Task<User?> GetByProviderKeyAsync(string providerUserKey, CancellationToken ct)
        {
            HttpResponseMessage response = await _httpClient.GetAsync($"/api/v1/accounts/users/by-provider-key/{Uri.EscapeDataString(providerUserKey)}", ct);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }

            _ = response.EnsureSuccessStatusCode();
            UserDto? dto = await response.Content.ReadFromJsonAsync<UserDto>(JsonOptions, ct);
            return dto?.ToDomain();
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<User>> ListAsync(CancellationToken ct)
        {
            HttpResponseMessage response = await _httpClient.GetAsync("/api/v1/accounts/users", ct);
            _ = response.EnsureSuccessStatusCode();
            List<UserDto>? dtos = await response.Content.ReadFromJsonAsync<List<UserDto>>(JsonOptions, ct);
            return (dtos ?? []).Select(x => x.ToDomain()).ToList();
        }

        /// <inheritdoc />
        public async Task AddAsync(User user, CancellationToken ct)
        {
            var request = new
            {
                user.ProviderUserKey,
                user.DisplayName,
                user.Email
            };

            HttpResponseMessage response = await _httpClient.PostAsJsonAsync("/api/v1/accounts/users", request, ct);
            _ = response.EnsureSuccessStatusCode();
        }

        /// <inheritdoc />
        public async Task UpdateAsync(User user, CancellationToken ct)
        {
            var request = new
            {
                user.DisplayName,
                user.Email
            };

            HttpResponseMessage response = await _httpClient.PutAsJsonAsync($"/api/v1/accounts/users/{user.Id}", request, ct);
            _ = response.EnsureSuccessStatusCode();
        }

        /// <inheritdoc />
        public async Task DeleteAsync(Guid userId, CancellationToken ct)
        {
            HttpResponseMessage response = await _httpClient.DeleteAsync($"/api/v1/accounts/users/{userId}", ct);
            _ = response.EnsureSuccessStatusCode();
        }
    }
}
