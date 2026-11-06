using System.Net.Http.Json;
using System.Text.Json;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Core.Api.Contracts;

namespace VideoForensics.Client.Hosting.Remote
{
    /// <summary>
    /// HTTP-backed, limited <see cref="IForensicsConfigurationService"/> that provides access to the server's
    /// read-only ClientConfigDto via the minimal API (see VideoForensics.WebApp/Api/ConfigEndpoints.cs).
    /// The IForensicsConfigurationService interface is designed for disk-based persistence (load/save from filesystem),
    /// which does not map to HTTP operations. Only a read-only server configuration fetch is available via
    /// GET /api/v1/config; all write and persistence methods throw <see cref="NotSupportedException"/>.
    /// </summary>
    public class RemoteForensicsConfigurationService : IForensicsConfigurationService
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly HttpClient _httpClient;

        public RemoteForensicsConfigurationService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <inheritdoc />
        /// <remarks>
        /// Returns the server's client-safe snapshot. Settings outside that allowlist keep their defaults, so callers must
        /// not treat the result as the full server configuration.
        /// </remarks>
        public async Task<IForensicsConfiguration> LoadConfigurationAsync(string configPath, CancellationToken cancellationToken = default)
        {
            ClientConfigDto? dto = await _httpClient.GetFromJsonAsync<ClientConfigDto>("/api/v1/config", JsonOptions, cancellationToken);
            ClientConfigDto snapshot = dto ?? throw new InvalidOperationException("Server returned no client configuration");
            return snapshot.ToDomain();
        }

        /// <inheritdoc />
        public Task SaveConfigurationAsync(IForensicsConfiguration config, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Not supported on a remote (client-hosted) configuration service - the IForensicsConfigurationService interface is designed for disk-based persistence, which is incompatible with HTTP access. Use the server's API directly, or implement filesystem-based configuration saving on the client.");
        }
    }
}
