using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Ui.Shared.Contracts
{
    /// <summary>
    /// Host-agnostic source of live-view session state pushes for the LiveView page. The page subscribes to a
    /// session and waits for it to become Active, instead of polling. It never talks to SignalR directly.
    /// <para>
    /// Pushes are raw and not replayed. A subscriber that needs the current state must read it once after it
    /// subscribes, which closes the gap between the read and the first push.
    /// </para>
    /// <para>
    /// Hosts with no live push channel report <see cref="IsConnected"/> as false, and the page polls instead.
    /// </para>
    /// </summary>
    public interface ILiveViewSessionSource
    {
        /// <summary>
        /// Session state pushes for every session this client has subscribed to. Subscribers must dispose
        /// their subscription.
        /// </summary>
        IObservable<LiveViewSession> SessionChanged { get; }

        /// <summary>
        /// True when the push channel is up. When false, <see cref="SubscribeAsync"/> cannot deliver pushes
        /// and callers should poll.
        /// </summary>
        bool IsConnected { get; }

        /// <summary>Asks the host to deliver pushes for <paramref name="sessionId"/>.</summary>
        /// <param name="sessionId">The live-view session to follow.</param>
        /// <param name="cancellationToken">Cancels the request.</param>
        Task SubscribeAsync(Guid sessionId, CancellationToken cancellationToken);

        /// <summary>Stops pushes for <paramref name="sessionId"/>.</summary>
        /// <param name="sessionId">The live-view session to stop following.</param>
        /// <param name="cancellationToken">Cancels the request.</param>
        Task UnsubscribeAsync(Guid sessionId, CancellationToken cancellationToken);
    }
}
