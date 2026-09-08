using System.Net.Http.Json;

using VideoForensics.Providers.Common.Contracts;

namespace VideoForensics.Hosting.Remote
{
    /// <summary>
    /// HTTP-backed <see cref="IMultiProviderAuthService"/> that lists providers via the server's
    /// GET /api/v1/auth/providers and hands back a <see cref="RemoteProviderAuthService"/> pinned to
    /// the chosen provider name for each subsequent login/2FA call.
    /// </summary>
    public class RemoteMultiProviderAuthService : IMultiProviderAuthService
    {
        private readonly HttpClient _httpClient;

        public RemoteMultiProviderAuthService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<IReadOnlyList<string>> GetAvailableProvidersAsync(CancellationToken cancellationToken = default)
        {
            List<string>? providers = await _httpClient.GetFromJsonAsync<List<string>>("/api/v1/auth/providers", cancellationToken);
            return providers ?? new List<string>();
        }

        public IProviderAuthService GetService(string providerName) => new RemoteProviderAuthService(_httpClient, providerName);
    }
}
