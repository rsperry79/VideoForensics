using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Serilog.Core;
using Serilog.Events;

namespace VideoForensics.Core.Logging.Serilog.Sinks
{
    /// <summary>
    /// A Serilog sink that writes log events to a Windows named pipe as newline-delimited JSON.
    /// Maintains a circular buffer of the last 500 entries in memory for client initialization.
    /// Gracefully handles client disconnect and pipe recreation. No-op on non-Windows platforms.
    /// </summary>
    public sealed class NamedPipeSink : ILogEventSink, IDisposable
    {
        private const string PipeName = "VideoForensics.Logger";
        private const int MaxBufferedEntries = 500;
        private const int PipeConnectTimeoutMs = 100; // Don't wait long for clients

        private readonly object _syncRoot = new();
        private readonly Queue<string> _circularBuffer = new();
        private NamedPipeServerStream? _pipeServer;
        private bool _disposed;

        public NamedPipeSink()
        {
        }

        /// <summary>
        /// Emits a log event to the named pipe and circular buffer.
        /// </summary>
        public void Emit(LogEvent logEvent)
        {
            if (_disposed)
                return;

            if (!OperatingSystem.IsWindows())
                return; // No-op on non-Windows platforms

            lock (_syncRoot)
            {
                string jsonLine = SerializeLogEvent(logEvent);

                // Add to circular buffer
                _circularBuffer.Enqueue(jsonLine);
                if (_circularBuffer.Count > MaxBufferedEntries)
                {
                    _circularBuffer.Dequeue();
                }

                // Attempt to write to pipe asynchronously
                _ = WriteToNamedPipeAsync(jsonLine);
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

        private async Task WriteToNamedPipeAsync(string jsonLine)
        {
            try
            {
                // Ensure pipe server exists or recreate if closed
                if (_pipeServer == null || !_pipeServer.IsConnected)
                {
                    _pipeServer?.Dispose();
                    _pipeServer = new NamedPipeServerStream(
                        PipeName,
                        PipeDirection.Out,
                        1,
                        PipeTransmissionMode.Byte,
                        PipeOptions.WriteThrough | PipeOptions.Asynchronous);
                }

                // Attempt to connect with timeout (non-blocking if no client)
                if (!_pipeServer.IsConnected)
                {
                    using (var cts = new CancellationTokenSource(PipeConnectTimeoutMs))
                    {
                        try
                        {
                            await _pipeServer.WaitForConnectionAsync(cts.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            // No client waiting; that's fine, just return
                            return;
                        }
                    }
                }

                // Write the log entry
                if (_pipeServer.IsConnected)
                {
                    byte[] buffer = Encoding.UTF8.GetBytes(jsonLine + Environment.NewLine);
                    await _pipeServer.WriteAsync(buffer, 0, buffer.Length);
                    await _pipeServer.FlushAsync();
                }
            }
            catch (ObjectDisposedException)
            {
                // Pipe was closed; will recreate on next log
            }
            catch (IOException)
            {
                // Client disconnected; will recreate on next log
            }
            catch (Exception ex)
            {
                // Log to console as failsafe; don't crash the logger
                Console.Error.WriteLine($"[NamedPipeSink] Error writing to pipe: {ex.Message}");
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
                _pipeServer?.Dispose();
                _disposed = true;
            }
        }
    }
}
