using System.Reactive.Linq;

using VideoForensics.Api.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting.Remote;
using VideoForensics.Ui.Shared.Contracts;

namespace VideoForensics.Hosting
{
    /// <summary>
    /// <see cref="ILiveViewSessionSource"/> over <see cref="RealtimeHub"/>. It maps each
    /// <see cref="LiveViewSessionDto"/> push to the domain entity, so Ui.Shared never sees the wire type.
    /// </summary>
    /// <remarks>
    /// The hub has no public live-view members on <see cref="Contracts.IRealtimeHub"/>, so this adapter takes the
    /// concrete hub. The hub is registered as a singleton and <see cref="Contracts.IRealtimeHub"/> forwards to it.
    /// </remarks>
    internal sealed class RealtimeLiveViewSessionSource : ILiveViewSessionSource
    {
        private readonly RealtimeHub _hub;

        /// <summary>Creates the source over the singleton realtime hub.</summary>
        public RealtimeLiveViewSessionSource(RealtimeHub hub)
        {
            _hub = hub ?? throw new ArgumentNullException(nameof(hub));
        }

        /// <inheritdoc />
        public IObservable<LiveViewSession> SessionChanged =>
            _hub.LiveViewSessionChanged.Select(dto => dto.ToDomain());

        /// <inheritdoc />
        public bool IsConnected => _hub.IsLiveHubConnected;

        /// <inheritdoc />
        public Task SubscribeAsync(Guid sessionId, CancellationToken cancellationToken) =>
            _hub.SubscribeLiveViewAsync(sessionId, cancellationToken);

        /// <inheritdoc />
        public Task UnsubscribeAsync(Guid sessionId, CancellationToken cancellationToken) =>
            _hub.UnsubscribeLiveViewAsync(sessionId, cancellationToken);
    }
}
