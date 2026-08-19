#nullable enable

using System;
using System.Threading;
using System.Threading.Tasks;

namespace KoenZomers.Ring.Api.Sockets
{
    /// <summary>
    /// Minimal websocket abstraction shared by RingAssetSocket (Alarm) and RingSignalingClient
    /// (WebRTC live view) so both can be unit tested with a fake transport, the same way HttpUtility
    /// accepts an injectable HttpMessageHandler for HTTP calls. Public so consumers of this library
    /// can also supply their own transport (e.g. for logging or a proxied connection) via the
    /// Session.ConnectAssetSocket/StartLiveView overloads that accept one.
    /// </summary>
    public interface IWebSocketTransport : IDisposable
    {
        Task ConnectAsync(Uri uri, CancellationToken cancellationToken);

        Task SendAsync(string message, CancellationToken cancellationToken);

        /// <summary>
        /// Returns the next complete text message received, or null if the connection closed.
        /// </summary>
        Task<string?> ReceiveAsync(CancellationToken cancellationToken);

        Task CloseAsync(CancellationToken cancellationToken);
    }
}
