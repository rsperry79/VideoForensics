using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace VideoForensics.Utils.LoggerViewer.Maui.Platforms.Linux
{
    /// <summary>
    /// Linux-specific IPC client using Unix domain sockets.
    /// Connects to /tmp/videoforensics.logger.sock
    /// </summary>
    public sealed class LinuxUnixSocketClient : IIpcClient
    {
        private const string SocketPath = "/tmp/videoforensics.logger.sock";
        private const int ConnectTimeoutMs = 5000;

        private Socket? _socket;
        private NetworkStream? _stream;
        private StreamReader? _reader;
        private bool _disposed;

        public bool IsConnected => _socket?.Connected ?? false;

        public async Task ConnectAsync(CancellationToken cancellationToken)
        {
            if (_disposed)
                throw new ObjectDisposedException(GetType().Name);

            if (IsConnected)
                return;

            _socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);

            try
            {
                var endpoint = new UnixDomainSocketEndPoint(SocketPath);
                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    cts.CancelAfter(ConnectTimeoutMs);
                    await _socket.ConnectAsync(endpoint, cts.Token);
                }

                _stream = new NetworkStream(_socket, ownsSocket: false);
                _reader = new StreamReader(_stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            }
            catch
            {
                _reader?.Dispose();
                _reader = null;
                _stream?.Dispose();
                _stream = null;
                _socket?.Dispose();
                _socket = null;
                throw;
            }
        }

        public async Task<string?> ReadLineAsync(CancellationToken cancellationToken)
        {
            if (_disposed)
                throw new ObjectDisposedException(GetType().Name);

            if (!IsConnected || _reader == null)
                throw new InvalidOperationException("Not connected.");

            try
            {
#if NET10_0_OR_GREATER
                var line = await _reader.ReadLineAsync(cancellationToken);
#else
                var line = await _reader.ReadLineAsync();
#endif
                return line;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // Connection lost
                await DisconnectAsync();
                throw;
            }
        }

        public async Task DisconnectAsync()
        {
            _reader?.Dispose();
            _reader = null;

            _stream?.Dispose();
            _stream = null;

            _socket?.Dispose();
            _socket = null;

            await Task.CompletedTask;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            DisconnectAsync().Wait();
            _disposed = true;
        }
    }
}
