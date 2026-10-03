using System;
using System.Threading;
using System.Threading.Tasks;

namespace VideoForensics.Utils.LoggerViewer.Maui.Platforms
{
    /// <summary>
    /// Abstracts cross-platform IPC (named pipes on Windows, Unix sockets on Linux).
    /// </summary>
    public interface IIpcClient : IDisposable
    {
        /// <summary>
        /// Gets whether the client is currently connected.
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Connects to the IPC endpoint asynchronously.
        /// </summary>
        Task ConnectAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Reads a line of text from the IPC endpoint.
        /// Returns null if end-of-stream is reached.
        /// </summary>
        Task<string?> ReadLineAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Disconnects from the IPC endpoint.
        /// </summary>
        Task DisconnectAsync();
    }
}
