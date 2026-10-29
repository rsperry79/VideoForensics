
using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace VideoForensics.Providers.Ring.Sockets
{
    /// <summary>
    /// Real IWebSocketTransport implementation backed by the BCL ClientWebSocket.
    /// </summary>
    internal class ClientWebSocketTransport : IWebSocketTransport
    {
        private readonly ClientWebSocket _socket = new();
        private const int ReceiveBufferSize = 16 * 1024;

        // The same app identity the REST calls send (HttpUtility). The signaling server sees the ticket's client, and a
        // socket without it is a different client than the one the ticket was issued for.
        private const string RingUserAgent = "android:com.ringapp";

        public ClientWebSocketTransport()
        {
            _socket.Options.SetRequestHeader("User-Agent", RingUserAgent);

            // Keeps the refused handshake's status and headers, which the plain "400 when 101 expected" message drops.
            _socket.Options.CollectHttpResponseDetails = true;
        }

        public async Task ConnectAsync(Uri uri, CancellationToken cancellationToken)
        {
            try
            {
                await _socket.ConnectAsync(uri, cancellationToken);
            }
            catch (WebSocketException) when (_socket.HttpStatusCode != default)
            {
                // The ticket is in the URL, so the message carries the status and correlation id only.
                string correlation = _socket.HttpResponseHeaders is not null
                    && _socket.HttpResponseHeaders.TryGetValue("X-Correlation-ID", out System.Collections.Generic.IEnumerable<string>? values)
                    ? string.Join(",", values)
                    : "none";
                throw new InvalidOperationException(
                    $"Ring signaling refused the live view connection (HTTP {(int)_socket.HttpStatusCode}, correlation {correlation}).");
            }
        }

        public async Task SendAsync(string message, CancellationToken cancellationToken)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(message);
            await _socket.SendAsync(bytes, WebSocketMessageType.Text, endOfMessage: true, cancellationToken);
        }

        public async Task<string?> ReceiveAsync(CancellationToken cancellationToken)
        {
            byte[] buffer = new byte[ReceiveBufferSize];
            using var stream = new System.IO.MemoryStream();

            while (true)
            {
                WebSocketReceiveResult result;
                try
                {
                    result = await _socket.ReceiveAsync(buffer, cancellationToken);
                }
                catch (WebSocketException)
                {
                    return null;
                }

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return null;
                }

                stream.Write(buffer, 0, result.Count);

                if (result.EndOfMessage)
                {
                    return Encoding.UTF8.GetString(stream.ToArray());
                }
            }
        }

        public async Task CloseAsync(CancellationToken cancellationToken)
        {
            if (_socket.State == WebSocketState.Open)
            {
                await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", cancellationToken);
            }
        }

        public void Dispose()
        {
            _socket.Dispose();
        }
    }
}
