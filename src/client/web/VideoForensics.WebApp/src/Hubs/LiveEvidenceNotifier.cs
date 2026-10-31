using Microsoft.AspNetCore.SignalR;

using VideoForensics.Data.Core.Contracts;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Core.Services;

namespace VideoForensics.Client.WebApp.Hubs
{
    /// <summary>
    /// Announces an evidence change to two audiences: the WebApp's own in-process views through
    /// <see cref="EvidenceChangedBus"/>, and every remote client connected to <see cref="LiveHub"/>.
    /// The hub already requires paired-device auth for each connection, so a broadcast reaches only authorized clients.
    /// </summary>
    public sealed class LiveEvidenceNotifier : IEvidenceChangeNotifier
    {
        private readonly IHubContext<LiveHub> _hubContext;
        private readonly EvidenceChangedBus _bus;
        private readonly ILogger<LiveEvidenceNotifier> _logger;

        /// <summary>Creates the notifier.</summary>
        public LiveEvidenceNotifier(IHubContext<LiveHub> hubContext, EvidenceChangedBus bus, ILogger<LiveEvidenceNotifier> logger)
        {
            _hubContext = hubContext;
            _bus = bus;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task EvidenceChangedAsync(Guid deviceId, CancellationToken ct)
        {
            var change = new EvidenceChangedDto(deviceId);
            _bus.Publish(change);
            try
            {
                await _hubContext.Clients.All.SendAsync(LiveHubMethods.EvidenceChanged, change, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // The write has already succeeded; a failed push only means open pages refresh on their next load.
                _logger.LogWarning(ex, "Could not push an evidence change for device {DeviceId} to live clients", deviceId);
            }
        }
    }
}
