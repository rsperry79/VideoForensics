using Makaretu.Dns;
using Microsoft.Extensions.Logging;

namespace VideoForensics.Hosting.ServerDiscovery
{
    /// <summary>
    /// Resolves the VideoForensics server address using mDNS service discovery on the local network.
    /// The discovery process looks for the _videoforensics._tcp service advertised by the server.
    ///
    /// Discovery flow:
    /// 1. Browses for _videoforensics._tcp service with a short timeout (default 3 seconds).
    /// 2. If found, builds a Uri using the discovered service's IP and port with http:// scheme.
    /// 3. If not found within timeout, falls back to cached Internet URL from IServerLocationSettingsStore.
    /// 4. If no cached URL exists, throws ServerNotReachableException.
    ///
    /// TRUST BOUNDARY: Uses http:// (not https://) for discovered local addresses because this app's
    /// NetworkTier.Local treats the LAN itself as the trust boundary - requiring certificate validation
    /// for a bare LAN IP or .local hostname is a known hard problem this codebase does not yet solve.
    /// The actual security boundary is the paired-device bearer token, not TLS for local traffic.
    /// Internet-facing (cached) addresses still use https:// since they traverse untrusted networks.
    /// </summary>
    public class ServerLocationResolver : IServerLocationResolver
    {
        private const string ServiceType = "_videoforensics._tcp.local";
        private static readonly TimeSpan DiscoveryTimeout = TimeSpan.FromSeconds(3);

        private readonly IServerLocationSettingsStore _settingsStore;
        private readonly ILogger<ServerLocationResolver> _logger;

        public ServerLocationResolver(
            IServerLocationSettingsStore settingsStore,
            ILogger<ServerLocationResolver> logger)
        {
            _settingsStore = settingsStore;
            _logger = logger;
        }

        public async Task<Uri> ResolveServerAddressAsync(CancellationToken cancellationToken)
        {
            // Attempt local discovery
            Uri? discoveredUri = await DiscoverLocalServerAsync(cancellationToken);
            if (discoveredUri != null)
            {
                _logger.LogInformation("Server discovered via mDNS at {Uri}", discoveredUri);
                return discoveredUri;
            }

            // Fall back to cached Internet URL
            string? cachedUrl = _settingsStore.GetCachedInternetServerUrl();
            if (!string.IsNullOrEmpty(cachedUrl))
            {
                _logger.LogInformation("Using cached Internet server URL: {Url}", cachedUrl);
                return new Uri(cachedUrl);
            }

            // No server found anywhere
            _logger.LogError("Server not reachable: local mDNS discovery failed and no cached Internet URL available");
            throw new ServerNotReachableException();
        }

        private async Task<Uri?> DiscoverLocalServerAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var mdns = new MulticastService();
                var discovery = new ServiceDiscovery(mdns);

                var tcs = new TaskCompletionSource<Uri?>();
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(DiscoveryTimeout);
                using var registration = timeoutCts.Token.Register(() => tcs.TrySetResult(null));

                discovery.ServiceInstanceDiscovered += (sender, e) =>
                {
                    try
                    {
                        // e.Message contains the full DNS response - look for SRV (port + target host)
                        // and A/AAAA records (the target host's IP) among its answers/additional records.
                        var srv = e.Message.AdditionalRecords.OfType<SRVRecord>().FirstOrDefault()
                            ?? e.Message.Answers.OfType<SRVRecord>().FirstOrDefault();
                        if (srv is null)
                        {
                            return;
                        }

                        var aRecord = e.Message.AdditionalRecords.OfType<ARecord>()
                            .FirstOrDefault(a => a.Name == srv.Target);
                        if (aRecord is null)
                        {
                            return;
                        }

                        var uri = new Uri($"http://{aRecord.Address}:{srv.Port}");
                        tcs.TrySetResult(uri);
                    }
                    catch
                    {
                        // Malformed/unexpected response shape for this instance - keep waiting for another,
                        // don't fault the whole discovery on one bad record.
                    }
                };

                mdns.Start();
                // Query for service instances: "_videoforensics._tcp" (no ".local" suffix)
                discovery.QueryServiceInstances("_videoforensics._tcp");

                Uri? result = await tcs.Task;
                mdns.Stop();
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "mDNS discovery failed, will attempt fallback");
                return null;
            }
        }
    }
}
