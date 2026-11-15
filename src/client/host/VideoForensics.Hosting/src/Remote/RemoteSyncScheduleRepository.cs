using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using VideoForensics.Core.Api.Contracts;
using VideoForensics.Core.Api.Contracts.Repositories;

namespace VideoForensics.Client.Hosting.Remote
{
    /// <summary>HTTP-backed <see cref="ISyncScheduleRepository"/> that calls the account sync schedule routes.</summary>
    public sealed class RemoteSyncScheduleRepository : ISyncScheduleRepository
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly HttpClient _httpClient;

        public RemoteSyncScheduleRepository(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <inheritdoc />
        public async Task<SyncScheduleDto?> GetByProviderAccountIdAsync(Guid providerAccountId, CancellationToken ct)
        {
            HttpResponseMessage response = await _httpClient.GetAsync($"/api/v1/accounts/provider-accounts/{providerAccountId}/sync-schedule", ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            _ = response.EnsureSuccessStatusCode();
            return await response.Content.ReadFromJsonAsync<SyncScheduleDto>(JsonOptions, ct);
        }

        /// <inheritdoc />
        public async Task UpsertAsync(SyncScheduleDto schedule, CancellationToken ct)
        {
            HttpResponseMessage response = await _httpClient.PutAsJsonAsync(
                $"/api/v1/accounts/provider-accounts/{schedule.ProviderAccountId}/sync-schedule", schedule, cancellationToken: ct);
            _ = response.EnsureSuccessStatusCode();
        }
    }
}
