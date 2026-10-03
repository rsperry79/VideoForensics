using System;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace VideoForensics.Utils.LoggerViewer.Maui.Platforms.Windows
{
    /// <summary>
    /// Windows-specific IPC client using named pipes.
    /// Connects to \\.\pipe\VideoForensics.Logger
    /// </summary>
    public sealed class WindowsNamedPipeClient : IIpcClient
    {
        private const string PipeName = "VideoForensics.Logger";
        private const string PipeServer = ".";
        private const int ConnectTimeoutMs = 5000;

        private NamedPipeClientStream? _pipe;
        private StreamReader? _reader;
        private bool _disposed;

        public bool IsConnected => _pipe?.IsConnected ?? false;

        public async Task ConnectAsync(CancellationToken cancellationToken)
        {
            if (_disposed)
                throw new ObjectDisposedException(GetType().Name);

            if (IsConnected)
                return;

            _pipe = new NamedPipeClientStream(PipeServer, PipeName, PipeDirection.In);

            try
            {
                await _pipe.ConnectAsync(ConnectTimeoutMs, cancellationToken);
                _reader = new StreamReader(_pipe, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            }
            catch
            {
                _pipe?.Dispose();
                _pipe = null;
                _reader = null;
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

            _pipe?.Dispose();
            _pipe = null;

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
