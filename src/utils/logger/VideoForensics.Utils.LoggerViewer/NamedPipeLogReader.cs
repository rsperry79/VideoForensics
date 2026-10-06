using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace VideoForensics.Utils.LoggerViewer
{
    /// <summary>
    /// Represents the connection state of the named pipe.
    /// </summary>
    public enum ConnectionState
    {
        Disconnected,
        Connecting,
        Connected,
        Reconnecting
    }

    /// <summary>
    /// Reads log entries from a named pipe and maintains a circular buffer.
    /// Implements auto-reconnect with exponential backoff.
    /// </summary>
    public class NamedPipeLogReader : IDisposable
    {
        private const string PipeName = "VideoForensics.Logger";
        private const int CircularBufferSize = 500;
        private const int InitialBackoffMs = 100;
        private const int MaxBackoffMs = 30000;

        private NamedPipeClientStream? _pipe;
        private StreamReader? _reader;
        private CancellationTokenSource _cancellationTokenSource = new();
        private Task? _readTask;
        private readonly CircularBuffer<LogEntry> _buffer = new(CircularBufferSize);
        private ConnectionState _connectionState = ConnectionState.Disconnected;
        private int _backoffMs = InitialBackoffMs;

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
        public ConnectionState ConnectionState => _connectionState;

        /// <summary>
        /// Gets all entries currently in the circular buffer.
        /// </summary>
        public IReadOnlyList<LogEntry> GetBufferedEntries()
        {
            lock (_buffer)
            {
                return _buffer.GetAll();
            }
        }

        /// <summary>
        /// Starts reading from the named pipe asynchronously.
        /// </summary>
        public async Task StartAsync()
        {
            if (_readTask != null && !_readTask.IsCompleted)
            {
                return;
            }

            _cancellationTokenSource = new();
            _readTask = ReadPipeAsync(_cancellationTokenSource.Token);
            await Task.Yield();
        }

        /// <summary>
        /// Stops reading from the named pipe.
        /// </summary>
        public async Task StopAsync()
        {
            _cancellationTokenSource.Cancel();
            if (_readTask != null)
            {
                try
                {
                    await _readTask;
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancellation is requested
                }
            }

            CloseConnection();
        }

        private async Task ReadPipeAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    SetConnectionState(ConnectionState.Connecting);

                    // Connect to the named pipe
                    _pipe = new NamedPipeClientStream(".", PipeName, PipeDirection.In);
                    await _pipe.ConnectAsync(5000, cancellationToken);

                    _reader = new StreamReader(_pipe, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                    _backoffMs = InitialBackoffMs;
                    SetConnectionState(ConnectionState.Connected);

                    // Read lines from the pipe
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        var line = await _reader.ReadLineAsync();
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
                                lock (_buffer)
                                {
                                    _buffer.Add(entry);
                                }
                                EntryReceived?.Invoke(entry);
                            }
                        }
                        catch (JsonException)
                        {
                            // Skip malformed entries
                            System.Diagnostics.Debug.WriteLine($"Failed to parse log entry: {line}");
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Pipe reading error: {ex.Message}");
                }
                finally
                {
                    CloseConnection();
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

        private void CloseConnection()
        {
            _reader?.Dispose();
            _reader = null;

            _pipe?.Dispose();
            _pipe = null;
        }

        private void SetConnectionState(ConnectionState state)
        {
            if (_connectionState != state)
            {
                _connectionState = state;
                ConnectionStateChanged?.Invoke(state);
            }
        }

        public void Dispose()
        {
            StopAsync().Wait();
            _cancellationTokenSource?.Dispose();
        }
    }

    /// <summary>
    /// A simple circular buffer implementation.
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
    }
}
