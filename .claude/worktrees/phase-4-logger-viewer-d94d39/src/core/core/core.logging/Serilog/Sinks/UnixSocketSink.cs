using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Serilog.Core;
using Serilog.Events;

namespace VideoForensics.Core.Logging.Serilog.Sinks
{
    /// <summary>
    /// A Serilog sink that writes log events to a Unix socket as newline-delimited JSON.
    /// Maintains a circular buffer of the last 500 entries in memory for client initialization.
    /// Gracefully handles client disconnect and socket recreation. No-op on non-Linux platforms.
    /// </summary>
    public sealed class UnixSocketSink : ILogEventSink, IDisposable
    {
        private const string SocketPath = "/tmp/videoforensics.logger.sock";
        private const int MaxBufferedEntries = 500;
        private const int SocketConnectTimeoutMs = 100; // Don't wait long for clients

        private readonly object _syncRoot = new();
        private readonly Queue<string> _circularBuffer = new();
        private Socket? _socket;
        private bool _disposed;

        public UnixSocketSink()
        {
        }

        /// <summary>
        /// Emits a log event to the Unix socket and circular buffer.
        /// </summary>
        public void Emit(LogEvent logEvent)
        {
            if (_disposed)
                return;

            if (!OperatingSystem.IsLinux())
                return; // No-op on non-Linux platforms

            lock (_syncRoot)
            {
                string jsonLine = SerializeLogEvent(logEvent);

                // Add to circular buffer
                _circularBuffer.Enqueue(jsonLine);
                if (_circularBuffer.Count > MaxBufferedEntries)
                {
                    _circularBuffer.Dequeue();
                }

                // Attempt to write to socket asynchronously
                _ = WriteToUnixSocketAsync(jsonLine);
            }
        }

        private static string SerializeLogEvent(LogEvent logEvent)
        {
            // Format the message using Serilog's formatter
            string message = logEvent.MessageTemplate.Render(logEvent.Properties);

            // Include exception if present
            if (logEvent.Exception != null)
            {
                message = $"{message}{Environment.NewLine}{logEvent.Exception}";
            }

            // Build JSON manually to avoid source generation issues
            var parts = new List<string>
            {
                $"\"timestamp\":\"{JsonEncode(logEvent.Timestamp.ToString("O"))}\"",
                $"\"level\":\"{JsonEncode(logEvent.Level.ToString())}\"",
                $"\"message\":\"{JsonEncode(message)}\"",
            };

            return "{" + string.Join(",", parts) + "}";
        }

        private static string JsonEncode(string value)
        {
            // Basic JSON string escaping
            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t");
        }

        private async Task WriteToUnixSocketAsync(string jsonLine)
        {
            try
            {
                // Ensure socket exists or recreate if closed
                if (_socket == null || !_socket.Connected)
                {
                    _socket?.Dispose();
                    _socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                    _socket.Blocking = false; // Non-blocking for async operations
                }

                // Attempt to connect with timeout (non-blocking if no client)
                if (!_socket.Connected)
                {
                    var endpoint = new UnixDomainSocketEndPoint(SocketPath);
                    using (var cts = new CancellationTokenSource(SocketConnectTimeoutMs))
                    {
                        try
                        {
                            await _socket.ConnectAsync(endpoint, cts.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            // No client waiting; that's fine, just return
                            return;
                        }
                        catch (IOException)
                        {
                            // Socket doesn't exist or client not listening; will retry on next log
                            return;
                        }
                    }
                }

                // Write the log entry
                if (_socket.Connected)
                {
                    byte[] buffer = Encoding.UTF8.GetBytes(jsonLine + Environment.NewLine);
                    await _socket.SendAsync(new ArraySegment<byte>(buffer), SocketFlags.None);
                }
            }
            catch (ObjectDisposedException)
            {
                // Socket was closed; will recreate on next log
            }
            catch (IOException)
            {
                // Client disconnected; will recreate on next log
            }
            catch (Exception ex)
            {
                // Log to console as failsafe; don't crash the logger
                Console.Error.WriteLine($"[UnixSocketSink] Error writing to socket: {ex.Message}");
            }
        }

        /// <summary>Returns a copy of the current circular buffer for client initialization.</summary>
        public string[] GetBufferedEntries()
        {
            lock (_syncRoot)
            {
                return _circularBuffer.ToArray();
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            lock (_syncRoot)
            {
                _socket?.Dispose();
                _disposed = true;
            }
        }
    }
}
