using Makaretu.Dns;

using Microsoft.Extensions.Logging;

namespace VideoForensics.Client.Hosting.ServerDiscovery
{
    /// <summary>
    /// Resolves the VideoForensics server address using mDNS service discovery on the local network.
    /// The discovery process looks for the _videoforensics._tcp service advertised by the server.
    ///
    /// Discovery flow:
    /// 0. Probes http://localhost:5162/healthz; a server on this device is used directly (same-device sign-in needs loopback).
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

        /// <summary>
        /// Where a server running on this same device is reached. Loopback matters: the server only applies its local
        /// password exemption to direct loopback requests, so a same-device client must not use the LAN address.
        /// Port matches the WebApp's default endpoint and the hot-swap health check.
        /// </summary>
        public static readonly Uri LoopbackServerUri = new("http://localhost:5162/");

        private readonly IServerLocationSettingsStore _settingsStore;
        private readonly ILogger<ServerLocationResolver> _logger;
        private readonly ILoopbackServerProbe _loopbackProbe;
        private readonly Func<CancellationToken, Task<Uri?>> _mdnsDiscovery;

        public ServerLocationResolver(
            IServerLocationSettingsStore settingsStore,
            ILogger<ServerLocationResolver> logger,
            ILoopbackServerProbe? loopbackProbe = null,
            Func<CancellationToken, Task<Uri?>>? mdnsDiscovery = null)
        {
            _settingsStore = settingsStore;
            _logger = logger;
            _loopbackProbe = loopbackProbe ?? new HttpLoopbackServerProbe();
            // Injectable so tests do not depend on what the LAN happens to advertise.
            _mdnsDiscovery = mdnsDiscovery ?? DiscoverLocalServerAsync;
        }

        public async Task<Uri> ResolveServerAddressAsync(CancellationToken cancellationToken)
        {
            // A server on this device wins over mDNS: the LAN address would make same-device sign-in look remote.
            if (await IsLoopbackServerRunningAsync(cancellationToken))
            {
                _logger.LogDebug("Server found on loopback");
                return LoopbackServerUri;
            }

            // Attempt local discovery
            Uri? discoveredUri = await _mdnsDiscovery(cancellationToken);
            if (discoveredUri != null)
            {
                _logger.LogDebug("Server discovered via mDNS");
                return discoveredUri;
            }

            // Fall back to cached Internet URL
            string? cachedUrl = _settingsStore.GetCachedInternetServerUrl();
            if (!string.IsNullOrEmpty(cachedUrl))
            {
                _logger.LogDebug("Using cached Internet server URL: {Url}", cachedUrl);
                return new Uri(cachedUrl);
            }

            // No server found anywhere
            _logger.LogError("Server not reachable: local mDNS discovery failed and no cached Internet URL available");
            throw new ServerNotReachableException();
        }

        private async Task<bool> IsLoopbackServerRunningAsync(CancellationToken cancellationToken)
        {
            try
            {
                return await _loopbackProbe.IsServerRunningAsync(LoopbackServerUri, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogDebug(ex, "Loopback probe failed; continuing with discovery");
                return false;
            }
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
                using CancellationTokenRegistration registration = timeoutCts.Token.Register(() => tcs.TrySetResult(null));

                discovery.ServiceInstanceDiscovered += (sender, e) =>
                {
                    try
                    {
                        // e.Message contains the full DNS response - look for SRV (port + target host)
                        // and A/AAAA records (the target host's IP) among its answers/additional records.
                        SRVRecord? srv = e.Message.AdditionalRecords.OfType<SRVRecord>().FirstOrDefault()
                            ?? e.Message.Answers.OfType<SRVRecord>().FirstOrDefault();
                        if (srv is null)
                        {
                            return;
                        }

                        ARecord? aRecord = e.Message.AdditionalRecords.OfType<ARecord>()
                            .FirstOrDefault(a => a.Name == srv.Target);
                        if (aRecord is null)
                        {
                            return;
                        }

                        var uri = new Uri($"http://{aRecord.Address}:{srv.Port}");
                        _ = tcs.TrySetResult(uri);
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
                _logger.LogError(ex, "mDNS discovery failed, will attempt fallback");
                return null;
            }
        }
    }
}
