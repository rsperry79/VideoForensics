namespace VideoForensics.Client.Hosting.ServerDiscovery
{
    /// <summary>Checks whether a VideoForensics server answers on the loopback address of this device.</summary>
    public interface ILoopbackServerProbe
    {
        /// <summary>Returns true when the server at <paramref name="baseUri"/> answers its health endpoint with success.</summary>
        Task<bool> IsServerRunningAsync(Uri baseUri, CancellationToken ct);
    }

    /// <summary>Probes the loopback health endpoint with a short timeout.</summary>
    public sealed class HttpLoopbackServerProbe : ILoopbackServerProbe, IDisposable
    {
        private readonly HttpClient _client = new() { Timeout = TimeSpan.FromSeconds(1) };

        /// <inheritdoc />
        public async Task<bool> IsServerRunningAsync(Uri baseUri, CancellationToken ct)
        {
            try
            {
                using HttpResponseMessage response = await _client.GetAsync(new Uri(baseUri, "healthz"), ct);
                return response.IsSuccessStatusCode;
            }
            catch (HttpRequestException)
            {
                return false;
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                // Per-request timeout: nothing answered on loopback.
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
