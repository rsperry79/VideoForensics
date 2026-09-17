using System.Net.Http.Json;
using System.Text.Json;

using VideoForensics.Api.Contracts;
using VideoForensics.Client.Common.Contracts;

namespace VideoForensics.Hosting.Remote
{
    /// <summary>
    /// HTTP-backed implementation of <see cref="IStorageSettingsService"/> that calls the server's Minimal API
    /// (see VideoForensics.WebApp/Api/StorageSettingsEndpoints.cs) instead of performing storage operations locally.
    /// The client (MAUI or other thin client) uses this as part of the client/server split.
    /// </summary>
    public sealed class RemoteStorageSettingsService : IStorageSettingsService
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly HttpClient _httpClient;

        public RemoteStorageSettingsService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <inheritdoc />
        public async Task<StorageSettings> GetStatusAsync(CancellationToken cancellationToken)
        {
            HttpResponseMessage response = await _httpClient.GetAsync("/api/v1/storage-settings", cancellationToken);
            _ = response.EnsureSuccessStatusCode();
            StorageSettingsDto? dto = await response.Content.ReadFromJsonAsync<StorageSettingsDto>(JsonOptions, cancellationToken);
            StorageSettingsDto result = dto ?? throw new InvalidOperationException("Server returned null storage settings");
            return result.ToDomain();
        }

        /// <inheritdoc />
        public async Task<RelocateStorageCategoryResult> RelocateAsync(RelocateStorageCategoryRequest request, CancellationToken cancellationToken)
        {
            RelocateStorageCategoryRequestDto requestDto = request.ToDto();
            HttpResponseMessage response = await _httpClient.PostAsJsonAsync("/api/v1/storage-settings/relocate", requestDto, cancellationToken);
            _ = response.EnsureSuccessStatusCode();
            RelocateStorageCategoryResultDto? dto = await response.Content.ReadFromJsonAsync<RelocateStorageCategoryResultDto>(JsonOptions, cancellationToken);
            RelocateStorageCategoryResultDto result = dto ?? throw new InvalidOperationException("Server returned null relocation result");
            return result.ToDomain();
        }
    }
}
