using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Ring.Api.Sockets;

namespace Ring.Api.Tests.Mocks
{
    /// <summary>
    /// Fake IWebSocketTransport for unit testing RingAssetSocket and RingSignalingClient without a
    /// real websocket connection. Records every sent message (SentMessages) and lets a test queue up
    /// canned incoming messages (Enqueue) to be delivered on the next ReceiveAsync call.
    /// </summary>
    public class FakeWebSocketTransport : IWebSocketTransport
    {
        public List<string> SentMessages { get; } = new();

        public Uri? ConnectedUri { get; private set; }

        /// <summary>
        /// Invoked synchronously from SendAsync, before it returns, for every message sent. Tests use
        /// this to Enqueue a canned response tied to the outgoing message's content - this is race-free
        /// because RingAssetSocket/RingSignalingClient always register their response waiter before
        /// calling SendAsync, so a response Enqueued here is guaranteed to be picked up.
        /// </summary>
        public Action<string>? OnMessageSent { get; set; }

        private readonly BlockingCollection<string> _incoming = new();

        public Task ConnectAsync(Uri uri, CancellationToken cancellationToken)
        {
            ConnectedUri = uri;
            return Task.CompletedTask;
        }

        public Task SendAsync(string message, CancellationToken cancellationToken)
        {
            lock (SentMessages)
            {
                SentMessages.Add(message);
            }
            OnMessageSent?.Invoke(message);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Queues a message to be returned by the next ReceiveAsync call.
        /// </summary>
        public void Enqueue(string message) => _incoming.Add(message);

        public Task<string?> ReceiveAsync(CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                try
                {
                    return _incoming.Take(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return null;
                }
            }, cancellationToken);
        }

        public Task CloseAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public void Dispose()
        {
            _incoming.Dispose();
        }
    }
}
