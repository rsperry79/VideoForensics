using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using VideoForensics.Utils.LoggerViewer.Maui.Models;
using VideoForensics.Utils.LoggerViewer.Maui.Platforms;

namespace VideoForensics.Utils.LoggerViewer.Maui.Services
{
    /// <summary>
    /// Connection state for the log reader.
    /// </summary>
    public enum ConnectionState
    {
        Disconnected,
        Connecting,
        Connected,
        Reconnecting
    }

    /// <summary>
    /// Manages connection to IPC (named pipe or Unix socket) and reads log entries.
    /// Implements auto-reconnect with exponential backoff.
    /// Thread-safe circular buffer of log entries.
    /// </summary>
    public class LogReaderService : IDisposable
    {
        private const int CircularBufferSize = 500;
        private const int InitialBackoffMs = 100;
        private const int MaxBackoffMs = 30000;

        private IIpcClient? _client;
        private CancellationTokenSource _cancellationTokenSource = new();
        private Task? _readTask;
        private readonly CircularBuffer<LogEntry> _buffer = new(CircularBufferSize);
        private ConnectionState _connectionState = ConnectionState.Disconnected;
        private int _backoffMs = InitialBackoffMs;
        private readonly object _syncLock = new();
        private bool _disposed;

        /// <summary>
        /// Raised when a new log entry is received.
        /// </summary>
        public event Action<LogEntry>? EntryReceived;

        /// <summary>
        /// Raised when the connection state changes.
        /// </summary>
        public event Action<ConnectionState>? ConnectionStateChanged;

        /// <summary>
        /// Gets the current connection state.
        /// </summary>
        public ConnectionState ConnectionState
        {
            get
            {
                lock (_syncLock)
                {
                    return _connectionState;
                }
            }
        }

        /// <summary>
        /// Gets all entries currently in the circular buffer.
        /// </summary>
        public IReadOnlyList<LogEntry> GetBufferedEntries()
        {
            lock (_syncLock)
            {
                return _buffer.GetAll();
            }
        }

        /// <summary>
        /// Starts reading from the IPC endpoint asynchronously.
        /// </summary>
        public async Task StartAsync()
        {
            if (_disposed)
                throw new ObjectDisposedException(GetType().Name);

            lock (_syncLock)
            {
                if (_readTask != null && !_readTask.IsCompleted)
                {
                    return;
                }

                _cancellationTokenSource = new();
                _readTask = ReadIpcAsync(_cancellationTokenSource.Token);
            }

            await Task.Yield();
        }

        /// <summary>
        /// Stops reading from the IPC endpoint.
        /// </summary>
        public async Task StopAsync()
        {
            lock (_syncLock)
            {
                _cancellationTokenSource.Cancel();
            }

            Task? readTask;
            lock (_syncLock)
            {
                readTask = _readTask;
            }

            if (readTask != null)
            {
                try
                {
                    await readTask;
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancellation is requested
                }
            }

            await CloseConnectionAsync();
        }

        private async Task ReadIpcAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    SetConnectionState(ConnectionState.Connecting);

                    // Create and connect to the IPC endpoint
                    _client = IpcClientFactory.CreateClient();
                    await _client.ConnectAsync(cancellationToken);

                    _backoffMs = InitialBackoffMs;
                    SetConnectionState(ConnectionState.Connected);

                    // Read lines from the IPC endpoint
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        try
                        {
                            var line = await _client.ReadLineAsync(cancellationToken);
                            if (line == null)
                            {
                                // End of stream, server closed the connection
                                break;
                            }

                            if (string.IsNullOrWhiteSpace(line))
                            {
                                continue;
                            }

                            try
                            {
                                var entry = JsonSerializer.Deserialize<LogEntry>(line);
                                if (entry != null)
                                {
                                    lock (_syncLock)
                                    {
                                        _buffer.Add(entry);
                                    }

                                    EntryReceived?.Invoke(entry);
                                }
                            }
                            catch (JsonException ex)
                            {
                                // Skip malformed entries; log for debugging
                                System.Diagnostics.Debug.WriteLine($"Failed to parse log entry: {line}. Error: {ex.Message}");
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                        catch
                        {
                            // Connection lost, will reconnect
                            break;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"IPC reading error: {ex.Message}");
                }
                finally
                {
                    await CloseConnectionAsync();
                }

                if (!cancellationToken.IsCancellationRequested)
                {
                    SetConnectionState(ConnectionState.Reconnecting);
                    try
                    {
                        await Task.Delay(_backoffMs, cancellationToken);
                        // Increase backoff, capped at MaxBackoffMs
                        _backoffMs = Math.Min(_backoffMs * 2, MaxBackoffMs);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }

            SetConnectionState(ConnectionState.Disconnected);
        }

        private async Task CloseConnectionAsync()
        {
            if (_client != null)
            {
                try
                {
                    await _client.DisconnectAsync();
                }
                catch
                {
                    // Ignore errors during disconnect
                }
                finally
                {
                    _client.Dispose();
                    _client = null;
                }
            }
        }

        private void SetConnectionState(ConnectionState state)
        {
            bool changed = false;
            lock (_syncLock)
            {
                if (_connectionState != state)
                {
                    _connectionState = state;
                    changed = true;
                }
            }

            if (changed)
            {
                ConnectionStateChanged?.Invoke(state);
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            StopAsync().Wait();
            lock (_syncLock)
            {
                _cancellationTokenSource?.Dispose();
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// A simple thread-safe circular buffer implementation.
    /// </summary>
    internal class CircularBuffer<T>
    {
        private readonly T[] _buffer;
        private int _index;
        private int _count;

        public CircularBuffer(int capacity)
        {
            _buffer = new T[capacity];
            _index = 0;
            _count = 0;
        }

        public void Add(T item)
        {
            _buffer[_index] = item;
            _index = (_index + 1) % _buffer.Length;
            if (_count < _buffer.Length)
            {
                _count++;
            }
        }

        public IReadOnlyList<T> GetAll()
        {
            var result = new List<T>(_count);
            if (_count == 0)
            {
                return result;
            }

            // If buffer is not full, return items from 0 to _index
            if (_count < _buffer.Length)
            {
                for (int i = 0; i < _count; i++)
                {
                    result.Add(_buffer[i]);
                }
            }
            else
            {
                // If buffer is full, return items starting from _index (oldest) in order
                for (int i = 0; i < _buffer.Length; i++)
                {
                    var idx = (_index + i) % _buffer.Length;
                    result.Add(_buffer[idx]);
                }
            }

            return result;
        }

        public void Clear()
        {
            _index = 0;
            _count = 0;
            Array.Clear(_buffer, 0, _buffer.Length);
        }
    }
}
