using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting;

namespace VideoForensics.WebApp.Services
{
    /// <summary>
    /// Starts the permanent Cloudflare tunnel at startup when a tunnel token is saved and the network tier is not
    /// Local. The network may not be ready at boot, so a failed start is retried a few times before giving up;
    /// the tunnel state then shows Failed with the error.
    /// </summary>
    public sealed class CloudflareTunnelAutostart : BackgroundService
    {
        private const int MaxAttempts = 3;
        private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(30);

        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<CloudflareTunnelAutostart> _logger;

        public CloudflareTunnelAutostart(IServiceScopeFactory scopes, ILogger<CloudflareTunnelAutostart> logger)
        {
            _scopes = scopes;
            _logger = logger;
        }

        /// <summary>The tunnel starts only when it is configured and the server is reachable beyond this machine.</summary>
        public static bool ShouldAutostart(NetworkTier tier, bool hasTunnelToken)
        {
            return hasTunnelToken && tier != NetworkTier.Local;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using IServiceScope scope = _scopes.CreateScope();
            IForensicsConfiguration config = scope.ServiceProvider.GetRequiredService<IForensicsConfiguration>();
            ICloudflareTokenStore tokens = scope.ServiceProvider.GetRequiredService<ICloudflareTokenStore>();
            ICloudflaredTunnelService tunnels = scope.ServiceProvider.GetRequiredService<ICloudflaredTunnelService>();

            string? token = await tokens.GetDecryptedTunnelTokenAsync(stoppingToken);
            if (!ShouldAutostart(config.ConfiguredNetworkTier, !string.IsNullOrWhiteSpace(token)))
            {
                _logger.LogInformation("Cloudflare tunnel autostart skipped: no tunnel token saved or the network tier is Local");
                return;
            }

            for (int attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                try
                {
                    await tunnels.StartPermanentTunnelAsync(token!, stoppingToken);
                    _logger.LogInformation("Cloudflare tunnel started at startup (attempt {Attempt})", attempt);
                    return;
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    if (attempt == MaxAttempts)
                    {
                        _logger.LogError(ex, "Cloudflare tunnel autostart failed after {Attempts} attempts", MaxAttempts);
                        return;
                    }

                    _logger.LogWarning(ex, "Cloudflare tunnel autostart attempt {Attempt} failed; retrying in {Seconds} seconds", attempt, RetryDelay.TotalSeconds);
                    await Task.Delay(RetryDelay, stoppingToken);
                }
            }
        }
    }
}
