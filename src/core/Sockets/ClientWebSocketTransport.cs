#nullable enable

using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KoenZomers.Ring.Api.Sockets
{
    /// <summary>
    /// Real IWebSocketTransport implementation backed by the BCL ClientWebSocket.
    /// </summary>
    internal class ClientWebSocketTransport : IWebSocketTransport
    {
        private readonly ClientWebSocket _socket = new();
        private const int ReceiveBufferSize = 16 * 1024;

        public async Task ConnectAsync(Uri uri, CancellationToken cancellationToken)
        {
            await _socket.ConnectAsync(uri, cancellationToken);
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
