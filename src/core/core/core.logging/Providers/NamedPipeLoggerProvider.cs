using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace VideoForensics.Core.Logging.Providers
{
    /// <summary>
    /// Writes log entries to a Windows named pipe as newline-delimited JSON for consumption
    /// by the Logger Viewer client. Maintains a circular buffer of the last 500 entries in
    /// memory for initial client connection. Gracefully handles client disconnect and pipe
    /// recreation. No-op on non-Windows platforms.
    /// </summary>
    public sealed class NamedPipeLoggerProvider : ILoggerProvider
    {
        private const string PipeName = @"\\.\pipe\VideoForensics.Logger";
        private const int MaxBufferedEntries = 500;
        private const int PipeConnectTimeoutMs = 100; // Don't wait long for clients

        private readonly object _syncRoot = new();
        private readonly Queue<string> _circularBuffer = new();
        private NamedPipeServerStream? _pipeServer;
        private bool _disposed;

        public NamedPipeLoggerProvider()
        {
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new NamedPipeLogger(categoryName, this);
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

        /// <summary>Internal: writes a log entry to the pipe and circular buffer.</summary>
        internal void WriteLog(LogEntry entry)
        {
            if (_disposed)
                return;

            if (!OperatingSystem.IsWindows())
                return; // No-op on non-Windows platforms

            lock (_syncRoot)
            {
                string jsonLine = SerializeLogEntry(entry);

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

        private static string SerializeLogEntry(LogEntry entry)
        {
            // Manual JSON serialization to avoid source generation issues
            var parts = new List<string>
            {
                $"\"timestamp\":\"{JsonEncode(entry.Timestamp)}\"",
                $"\"level\":\"{JsonEncode(entry.Level)}\"",
                $"\"category\":\"{JsonEncode(entry.Category)}\"",
                $"\"message\":\"{JsonEncode(entry.Message)}\""
            };

            // Only include eventId if non-zero
            if (entry.EventId != 0)
            {
                parts.Add($"\"eventId\":{entry.EventId}");
            }

            // Only include eventName if not null
            if (!string.IsNullOrEmpty(entry.EventName))
            {
                parts.Add($"\"eventName\":\"{JsonEncode(entry.EventName)}\"");
            }

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
                        "VideoForensics.Logger",
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
                    byte[] buffer = System.Text.Encoding.UTF8.GetBytes(jsonLine + Environment.NewLine);
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
                Console.Error.WriteLine($"[NamedPipeLogger] Error writing to pipe: {ex.Message}");
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

        private sealed class NamedPipeLogger : ILogger
        {
            private readonly string _categoryName;
            private readonly NamedPipeLoggerProvider _provider;

            public NamedPipeLogger(string categoryName, NamedPipeLoggerProvider provider)
            {
                _categoryName = categoryName;
                _provider = provider;
            }

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            {
                return null;
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return logLevel != LogLevel.None;
            }

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                if (!IsEnabled(logLevel))
                {
                    return;
                }

                string message = formatter(state, exception);
                if (exception != null)
                {
                    message = $"{message}{Environment.NewLine}{exception}";
                }

                var entry = new LogEntry
                {
                    Timestamp = DateTime.UtcNow.ToString("O"), // ISO 8601 format
                    Level = logLevel.ToString(),
                    Category = _categoryName,
                    Message = message,
                    EventId = eventId.Id,
                    EventName = eventId.Name
                };

                _provider.WriteLog(entry);
            }
        }
    }

    /// <summary>Log entry DTO for JSON serialization.</summary>
    public sealed class LogEntry
    {
        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; } = string.Empty;

        [JsonPropertyName("level")]
        public string Level { get; set; } = string.Empty;

        [JsonPropertyName("category")]
        public string Category { get; set; } = string.Empty;

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;

        [JsonPropertyName("eventId")]
        public int EventId { get; set; }

        [JsonPropertyName("eventName")]
        public string? EventName { get; set; }
    }
}
