using Microsoft.AspNetCore.SignalR;

using VideoForensics.Data.Core.Contracts;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Core.Services;

namespace VideoForensics.Client.WebApp.Hubs
{
    /// <summary>
    /// Announces a produced thumbnail to two audiences: the WebApp's own in-process views through
    /// <see cref="ThumbnailReadyBus"/>, and every remote client connected to <see cref="LiveHub"/>.
    /// The hub already requires paired-device auth for each connection, so a broadcast reaches only authorized clients.
    /// </summary>
    public sealed class LiveThumbnailNotifier : IMediaThumbnailNotifier
    {
        private readonly IHubContext<LiveHub> _hubContext;
        private readonly ThumbnailReadyBus _bus;

        /// <summary>Creates the notifier.</summary>
        public LiveThumbnailNotifier(IHubContext<LiveHub> hubContext, ThumbnailReadyBus bus)
        {
            _hubContext = hubContext;
            _bus = bus;
        }

        /// <inheritdoc />
        public async Task ThumbnailReadyAsync(Guid mediaItemId, CancellationToken ct)
        {
            var ready = new ThumbnailReadyDto(mediaItemId);
            _bus.Publish(ready);
            await _hubContext.Clients.All.SendAsync(LiveHubMethods.ThumbnailReady, ready, ct);
        }
    }
}
