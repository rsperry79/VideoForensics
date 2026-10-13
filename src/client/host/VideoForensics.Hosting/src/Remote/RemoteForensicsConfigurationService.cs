using VideoForensics.Client.Common.Contracts;

namespace VideoForensics.Hosting.Remote
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
        private readonly HttpClient _httpClient;

        public RemoteForensicsConfigurationService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <inheritdoc />
        public Task<IForensicsConfiguration> LoadConfigurationAsync(string configPath, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Not supported on a remote (client-hosted) configuration service - the IForensicsConfigurationService interface is designed for disk-based persistence, which is incompatible with HTTP access. Use the server's API directly (GET /api/v1/config) to fetch read-only client configuration, or implement filesystem-based configuration loading on the client.");
        }

        /// <inheritdoc />
        public Task SaveConfigurationAsync(IForensicsConfiguration config, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Not supported on a remote (client-hosted) configuration service - the IForensicsConfigurationService interface is designed for disk-based persistence, which is incompatible with HTTP access. Use the server's API directly, or implement filesystem-based configuration saving on the client.");
        }
    }
}
