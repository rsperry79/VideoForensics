namespace VideoForensics.Client.Platform.Windows
{
    /// <summary>Probes the local WebApp health endpoint with a short per-request timeout.</summary>
    public sealed class WebAppHealthProbe : IWebAppHealthProbe, IDisposable
    {
        private const string HealthUrl = "http://localhost:5162/healthz";

        private readonly HttpClient _client = new() { Timeout = TimeSpan.FromMilliseconds(1500) };

        /// <inheritdoc />
        public async Task<bool> IsHealthyAsync(CancellationToken ct)
        {
            try
            {
                using HttpResponseMessage response = await _client.GetAsync(HealthUrl, HttpCompletionOption.ResponseHeadersRead, ct);
                return response.IsSuccessStatusCode;
            }
            catch (HttpRequestException)
            {
                // Not listening yet, or the connection was refused.
                return false;
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                // Per-request timeout (not the caller's cancellation): treat as not ready yet.
                return false;
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _client.Dispose();
        }
    }
}
