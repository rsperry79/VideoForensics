using Microsoft.AspNetCore.SignalR;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Core.Api.Contracts;

namespace VideoForensics.Client.WebApp.Hubs
{
    /// <summary>
    /// Announces a saved configuration to two audiences: the WebApp's own in-process views through
    /// <see cref="ClientConfigChangedBus"/>, and every remote client connected to <see cref="LiveHub"/>.
    /// The payload is the client-safe <see cref="ClientConfigDto"/> allowlist, never the full configuration.
    /// </summary>
    public sealed class LiveConfigNotifier : IConfigurationChangeNotifier
    {
        private readonly IHubContext<LiveHub> _hubContext;
        private readonly ClientConfigChangedBus _bus;
        private readonly ILogger<LiveConfigNotifier> _logger;

        /// <summary>Creates the notifier.</summary>
        public LiveConfigNotifier(IHubContext<LiveHub> hubContext, ClientConfigChangedBus bus, ILogger<LiveConfigNotifier> logger)
        {
            _hubContext = hubContext;
            _bus = bus;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task ConfigurationSavedAsync(IForensicsConfiguration config, CancellationToken cancellationToken)
        {
            ClientConfigDto snapshot = config.ToDto();
            _bus.Publish(snapshot);
            try
            {
                await _hubContext.Clients.All.SendAsync(LiveHubMethods.ClientConfigChanged, snapshot, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // The save has already succeeded; a failed push only means remote settings views update on their next load.
                _logger.LogWarning(ex, "Could not push a settings change to live clients");
            }
        }
    }
}
