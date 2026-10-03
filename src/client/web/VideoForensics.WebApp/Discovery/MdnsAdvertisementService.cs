using Makaretu.Dns;

using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

using VideoForensics.Client.Common.Contracts;

namespace VideoForensics.WebApp.Discovery
{
    /// <summary>
    /// Advertises this server on the LAN via mDNS/DNS-SD (plan §5.2) as
    /// <c>_videoforensics._tcp.local</c>, so a pairing client on the same network can discover its
    /// address without the owner typing an IP into the QR-pairing screen. The advertisement itself
    /// carries no credentials and grants no access - it only says "a VideoForensics server is
    /// reachable at this address," exactly the same information a QR code's URL already exposes to
    /// anyone on the LAN who scans it.
    ///
    /// Config-gated (<see cref="IForensicsConfiguration.EnableMdnsAdvertisement"/>, default on) and
    /// re-checked on a timer rather than once at startup, so toggling it in Settings takes effect
    /// without restarting the server - the same responsiveness <c>DeviceHealthSyncService</c> gives
    /// its own toggle.
    /// </summary>
    public class MdnsAdvertisementService : BackgroundService
    {
        private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(15);

        private readonly IForensicsConfiguration _config;
        private readonly IServer _server;
        private readonly IHostApplicationLifetime _lifetime;
        private readonly ILogger<MdnsAdvertisementService> _logger;

        private MulticastService? _mdns;
        private ServiceDiscovery? _serviceDiscovery;

        public MdnsAdvertisementService(
            IForensicsConfiguration config,
            IServer server,
            IHostApplicationLifetime lifetime,
            ILogger<MdnsAdvertisementService> logger)
        {
            _config = config;
            _server = server;
            _lifetime = lifetime;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await WaitForApplicationStartedAsync(stoppingToken);

            using var timer = new PeriodicTimer(CheckInterval);
            do
            {
                try
                {
                    if (_config.EnableMdnsAdvertisement && _serviceDiscovery is null)
                    {
                        await StartAdvertisingAsync();
                    }
                    else if (!_config.EnableMdnsAdvertisement && _serviceDiscovery is not null)
                    {
                        await StopAdvertisingAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "mDNS advertisement check failed");
                }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            await StopAdvertisingAsync();
            await base.StopAsync(cancellationToken);
        }

        private async Task WaitForApplicationStartedAsync(CancellationToken ct)
        {
            var startedSource = new TaskCompletionSource();
            using CancellationTokenRegistration registration = _lifetime.ApplicationStarted.Register(() => startedSource.TrySetResult());
            if (_lifetime.ApplicationStarted.IsCancellationRequested)
            {
                return;
            }

            await startedSource.Task.WaitAsync(ct);
        }

        private async Task StartAdvertisingAsync()
        {
            int? port = ResolveListeningPort();
            if (port is null)
            {
                _logger.LogWarning("Could not determine the server's listening port; mDNS advertisement skipped");
                return;
            }

            try
            {
                _mdns = new MulticastService();
                _serviceDiscovery = new ServiceDiscovery(_mdns);

                var profile = new ServiceProfile(Environment.MachineName, "_videoforensics._tcp", (ushort)port.Value);
                _serviceDiscovery.Advertise(profile);
                _mdns.Start();

                _logger.LogInformation("mDNS advertisement started: {Instance}.{Service} on port {Port}",
                    Environment.MachineName, "_videoforensics._tcp.local", port.Value);

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start mDNS advertisement");
                _mdns?.Dispose();
                _serviceDiscovery?.Dispose();
                _mdns = null;
                _serviceDiscovery = null;
            }
        }

        private async Task StopAdvertisingAsync()
        {
            if (_serviceDiscovery is null)
            {
                return;
            }

            try
            {
                _serviceDiscovery.Unadvertise();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while unadvertising mDNS service");
            }
            finally
            {
                _mdns?.Stop();
                _mdns?.Dispose();
                _serviceDiscovery?.Dispose();
                _mdns = null;
                _serviceDiscovery = null;
                _logger.LogInformation("mDNS advertisement stopped");
            }

            await Task.CompletedTask;
        }

        private int? ResolveListeningPort()
        {
            ICollection<string>? addresses = _server.Features.Get<IServerAddressesFeature>()?.Addresses;
            if (addresses is null)
            {
                return null;
            }

            foreach (string address in addresses)
            {
                if (Uri.TryCreate(address, UriKind.Absolute, out Uri? uri))
                {
                    return uri.Port;
                }
            }

            return null;
        }

        public override void Dispose()
        {
            // Synchronously stop advertising since Dispose is not async
            // StopAsync is called by the framework, but we also ensure cleanup here
            if (_serviceDiscovery is not null)
            {
                try
                {
                    _serviceDiscovery.Unadvertise();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during Dispose while unadvertising");
                }
                finally
                {
                    _mdns?.Stop();
                    _mdns?.Dispose();
                    _serviceDiscovery?.Dispose();
                    _mdns = null;
                    _serviceDiscovery = null;
                }
            }

            base.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
