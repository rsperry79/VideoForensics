using Microsoft.Extensions.Logging;

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Channels;
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
        private const int MaxClients = 2;
        private const int MaxQueuedLines = 1000;
        private const int FullPollDelayMs = 500;
        private const int AcceptRetryDelayMs = 5000;

        private readonly object _syncRoot = new();
        private readonly Queue<string> _circularBuffer = new();
        private readonly List<NamedPipeServerStream> _clients = new();

        // One queue and one writer keep lines in order; oldest lines drop if no client drains fast enough.
        private readonly Channel<string> _queue = Channel.CreateBounded<string>(
            new BoundedChannelOptions(MaxQueuedLines) { FullMode = BoundedChannelFullMode.DropOldest });

        private CancellationTokenSource? _loopCts;
        private readonly NamedPipeLogGate? _gate;
        private bool _disposed;

        /// <param name="gate">Optional runtime switch. Null leaves the pipe always open, as before.</param>
        public NamedPipeLoggerProvider(NamedPipeLogGate? gate = null)
        {
            _gate = gate;
            if (_gate is not null)
            {
                _gate.Changed += OnGateChanged;
            }
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new NamedPipeLogger(categoryName, this);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            if (_gate is not null)
            {
                _gate.Changed -= OnGateChanged;
            }

            lock (_syncRoot)
            {
                _disposed = true;
                StopLoops();
            }
        }

        // Caller holds _syncRoot. Stops the accept/write loops and closes every client.
        private void StopLoops()
        {
            _loopCts?.Cancel();
            _loopCts?.Dispose();
            _loopCts = null;

            foreach (NamedPipeServerStream client in _clients)
            {
                client.Dispose();
            }

            _clients.Clear();
        }

        // Caller holds _syncRoot. Starts the loops on first use, or after a closed gate reopens.
        private void EnsureStarted()
        {
            if (_loopCts is not null)
            {
                return;
            }

            _loopCts = new CancellationTokenSource();
            CancellationToken token = _loopCts.Token;
            _ = Task.Run(() => AcceptLoopAsync(token), token);
            _ = Task.Run(() => WriteLoopAsync(token), token);
        }

        // Releases the pipe the moment the gate closes, so a closed gate leaves no pipe for clients to connect to.
        private void OnGateChanged(object? sender, EventArgs e)
        {
            if (_gate is { IsOpen: true })
            {
                return;
            }

            lock (_syncRoot)
            {
                StopLoops();
            }
        }

        /// <summary>Internal: writes a log entry to the pipe and circular buffer.</summary>
        internal void WriteLog(LogEntry entry)
        {
            if (_disposed || _gate is { IsOpen: false })
            {
                return;
            }

            if (!OperatingSystem.IsWindows())
            {
                return; // No-op on non-Windows platforms
            }

            lock (_syncRoot)
            {
                string jsonLine = SerializeLogEntry(entry);

                // Add to circular buffer
                _circularBuffer.Enqueue(jsonLine);
                if (_circularBuffer.Count > MaxBufferedEntries)
                {
                    _ = _circularBuffer.Dequeue();
                }

                EnsureStarted();
                _ = _queue.Writer.TryWrite(jsonLine);
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

        // Keeps one instance waiting while fewer than MaxClients are connected, so several viewers can attach.
        private async Task AcceptLoopAsync(CancellationToken token)
        {
            bool acceptErrorReported = false;
            while (!token.IsCancellationRequested)
            {
                try
                {
                    bool full;
                    lock (_syncRoot)
                    {
                        PruneDisconnectedClients();
                        full = _clients.Count >= MaxClients;
                    }

                    if (full)
                    {
                        await Task.Delay(FullPollDelayMs, token);
                        continue;
                    }

                    var server = new NamedPipeServerStream(
                        "VideoForensics.Logger",
                        PipeDirection.Out,
                        MaxClients,
                        PipeTransmissionMode.Byte,
                        PipeOptions.WriteThrough | PipeOptions.Asynchronous);

                    try
                    {
                        await server.WaitForConnectionAsync(token);
                    }
                    catch
                    {
                        server.Dispose();
                        throw;
                    }

                    lock (_syncRoot)
                    {
                        if (token.IsCancellationRequested)
                        {
                            server.Dispose();
                            return;
                        }

                        _clients.Add(server);
                    }
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception ex) when (ex is IOException or ObjectDisposedException)
                {
                    // Client dropped during connect or pipe was closed; retry shortly
                    await Task.Delay(FullPollDelayMs, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    // Console failsafe; don't crash the logger. Reported once: another process may own the pipe name.
                    if (!acceptErrorReported)
                    {
                        acceptErrorReported = true;
                        Console.Error.WriteLine($"[NamedPipeLogger] Error accepting pipe client ({ex.GetType().Name}): {ex.Message}");
                    }

                    await Task.Delay(AcceptRetryDelayMs, CancellationToken.None);
                }
            }
        }

        private async Task WriteLoopAsync(CancellationToken token)
        {
            try
            {
                await foreach (string jsonLine in _queue.Reader.ReadAllAsync(token))
                {
                    NamedPipeServerStream[] clients;
                    lock (_syncRoot)
                    {
                        clients = _clients.ToArray();
                    }

                    if (clients.Length == 0)
                    {
                        continue; // Still held in the circular buffer
                    }

                    byte[] buffer = System.Text.Encoding.UTF8.GetBytes(jsonLine + Environment.NewLine);
                    foreach (NamedPipeServerStream client in clients)
                    {
                        try
                        {
                            await client.WriteAsync(buffer, token);
                            await client.FlushAsync(token);
                        }
                        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
                        {
                            RemoveClient(client);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Gate closed or provider disposed
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[NamedPipeLogger] Error writing to pipe ({ex.GetType().Name}): {ex.Message}");
            }
        }

        // Caller holds _syncRoot.
        private void PruneDisconnectedClients()
        {
            for (int i = _clients.Count - 1; i >= 0; i--)
            {
                if (!_clients[i].IsConnected)
                {
                    _clients[i].Dispose();
                    _clients.RemoveAt(i);
                }
            }
        }

        private void RemoveClient(NamedPipeServerStream client)
        {
            lock (_syncRoot)
            {
                if (_clients.Remove(client))
                {
                    client.Dispose();
                }
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
