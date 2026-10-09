using Microsoft.AspNetCore.SignalR;

using VideoForensics.Api.Contracts;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.WebApp.Hubs
{
    /// <summary>
    /// SignalR-backed <see cref="ILiveViewTelemetryPublisher"/>: pushes a live-view session's state to
    /// the connections subscribed to that session over <see cref="LiveHub"/>.
    /// <para>
    /// Only the session's own group receives the message (see <see cref="LiveHubMethods.LiveViewGroup"/>),
    /// never all clients. With no subscribers the group send is a no-op, so no special case is needed.
    /// </para>
    /// </summary>
    public class LiveViewTelemetryBroadcastService : ILiveViewTelemetryPublisher
    {
        private readonly IHubContext<LiveHub> _hubContext;

        public LiveViewTelemetryBroadcastService(IHubContext<LiveHub> hubContext)
        {
            _hubContext = hubContext;
        }

        /// <inheritdoc />
        public Task PublishSessionChangedAsync(LiveViewSession session, CancellationToken ct)
        {
            return _hubContext.Clients
                .Group(LiveHubMethods.LiveViewGroup(session.Id))
                .SendAsync(LiveHubMethods.LiveViewSessionChanged, session.ToDto(), ct);
        }
    }
}
