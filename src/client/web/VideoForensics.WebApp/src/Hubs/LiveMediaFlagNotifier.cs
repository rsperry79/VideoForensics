using Microsoft.AspNetCore.SignalR;

using VideoForensics.Core.Api.Contracts;

namespace VideoForensics.Client.WebApp.Hubs
{
    /// <summary>
    /// Announces a media flag change to two audiences: the WebApp's own in-process views through <see cref="MediaFlagChangedBus"/>,
    /// and every remote client connected to <see cref="LiveHub"/>. The hub already requires paired-device auth for each
    /// connection, so a broadcast reaches only authorized clients.
    /// </summary>
    public sealed class LiveMediaFlagNotifier : IMediaFlagChangeNotifier
    {
        private readonly IHubContext<LiveHub> _hubContext;
        private readonly MediaFlagChangedBus _bus;
        private readonly ILogger<LiveMediaFlagNotifier> _logger;

        /// <summary>Creates the notifier.</summary>
        public LiveMediaFlagNotifier(IHubContext<LiveHub> hubContext, MediaFlagChangedBus bus, ILogger<LiveMediaFlagNotifier> logger)
        {
            _hubContext = hubContext;
            _bus = bus;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task MediaFlagChangedAsync(Guid flagId, Guid deviceId, CancellationToken ct)
        {
            var change = new MediaFlagChangedDto(flagId, deviceId);
            _bus.Publish(change);
            try
            {
                await _hubContext.Clients.All.SendAsync(LiveHubMethods.MediaFlagChanged, change, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // The change is already stored; a failed push only means open pages show the new status on their next load.
                _logger.LogWarning("Could not push a media flag change for flag {FlagId} to live clients: {ErrorType} {ErrorMessage}", flagId, ex.GetType().Name, ex.Message);
            }
        }
    }
}
