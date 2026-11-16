using System.Net.Http.Json;
using System.Text.Json;

using VideoForensics.Core.Api.Contracts.Repositories;

namespace VideoForensics.Client.Hosting.Remote
{
    /// <summary>
    /// HTTP-backed <see cref="IOperatorRepository"/>. Reads the first-run flag from the anonymous <c>/api/v1/setup/status</c>
    /// route, because the pairing page checks it before any operator has signed in.
    /// </summary>
    public sealed class RemoteOperatorRepository : IOperatorRepository
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly HttpClient _httpClient;

        public RemoteOperatorRepository(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <inheritdoc />
        public async Task<bool> IsEmptyAsync(CancellationToken ct)
        {
            HttpResponseMessage response = await _httpClient.GetAsync("/api/v1/setup/status", ct);
            _ = response.EnsureSuccessStatusCode();
            SetupStatus? status = await response.Content.ReadFromJsonAsync<SetupStatus>(JsonOptions, ct);
            return status?.IsEmpty ?? false;
        }

        private sealed record SetupStatus(bool IsEmpty, bool IsLocal);
    }
}
