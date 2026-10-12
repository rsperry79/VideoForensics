using Microsoft.Extensions.Logging;

using System;

using VideoForensics.Core.Logging.Services;

namespace VideoForensics.Core.Logging.Providers
{
    /// <summary>
    /// A logging provider that captures log entries into an in-memory buffer.
    /// Messages are redacted for sensitive data before entry into the buffer.
    /// Exception text is truncated to 4000 characters.
    /// </summary>
    public sealed class InMemoryLogBufferProvider : ILoggerProvider
    {
        private readonly InMemoryLogBuffer _buffer;
        private volatile bool _disposed;

        /// <summary>
        /// Initializes a new instance with the given buffer.
        /// </summary>
        public InMemoryLogBufferProvider(InMemoryLogBuffer buffer)
        {
            _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
        }

        /// <summary>
        /// Creates a logger for the given category name.
        /// Returns a no-op logger if the provider has been disposed.
        /// </summary>
        public ILogger CreateLogger(string categoryName)
        {
            if (_disposed)
                return new NoOpLogger();

            return new InMemoryLogBufferLogger(categoryName, _buffer, this);
        }

        /// <summary>
        /// Disposes the provider (does not dispose the buffer, as it may be shared).
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
        }

        private sealed class NoOpLogger : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => false;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
        }

        private sealed class InMemoryLogBufferLogger : ILogger
        {
            private readonly string _categoryName;
            private readonly InMemoryLogBuffer _buffer;
            private readonly InMemoryLogBufferProvider _provider;

            public InMemoryLogBufferLogger(string categoryName, InMemoryLogBuffer buffer, InMemoryLogBufferProvider provider)
            {
                _categoryName = categoryName;
                _buffer = buffer;
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
                if (!IsEnabled(logLevel) || _provider._disposed)
                    return;

                // Format the message
                string message = formatter(state, exception);

                // Redact sensitive data
                message = LogRedactor.Redact(message) ?? message;

                // Format exception text (type + message + stack)
                string? exceptionText = null;
                if (exception != null)
                {
                    exceptionText = FormatException(exception);
                    // Truncate to 4000 characters
                    if (exceptionText.Length > 4000)
                    {
                        exceptionText = exceptionText.Substring(0, 4000);
                    }
                }

                // Create the log record
                var record = new LogRecord(
                    Sequence: 0, // Will be assigned by buffer
                    TimestampUtc: DateTimeOffset.UtcNow,
                    Level: logLevel.ToString(),
                    Category: _categoryName,
                    Message: message,
                    Exception: exceptionText);

                // Append to buffer
                _buffer.Append(ref record);
            }

            private static string FormatException(Exception exception)
            {
                // Format: Type: Message + newline + StackTrace
                var result = $"{exception.GetType().FullName}: {exception.Message}";
                if (!string.IsNullOrEmpty(exception.StackTrace))
                {
                    result += $"{Environment.NewLine}{exception.StackTrace}";
                }

                if (exception.InnerException != null)
                {
                    result += $"{Environment.NewLine}--- Inner Exception ---{Environment.NewLine}{FormatException(exception.InnerException)}";
                }

                return result;
            }
        }
    }
}
