using Microsoft.Extensions.Logging;

using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;

using System;

using VideoForensics.Core.Logging.Serilog.Sinks;

namespace VideoForensics.Core.Logging.Providers
{
    /// <summary>
    /// A Microsoft.Extensions.Logging ILoggerProvider that wraps a Serilog UnixSocketSink
    /// for Logger Viewer client consumption. Linux-only; no-op on non-Linux platforms.
    /// </summary>
    public sealed class SerilogUnixSocketLoggerProvider : ILoggerProvider
    {
        private readonly UnixSocketSink _sink;
        private bool _disposed;

        public SerilogUnixSocketLoggerProvider()
        {
            _sink = new UnixSocketSink();
        }

        Microsoft.Extensions.Logging.ILogger ILoggerProvider.CreateLogger(string categoryName)
        {
            return new SerilogUnixSocketLogger(_sink);
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _sink?.Dispose();
            _disposed = true;
        }

        /// <summary>Returns a copy of the current circular buffer for client initialization.</summary>
        public string[] GetBufferedEntries()
        {
            return _sink.GetBufferedEntries();
        }

        /// <summary>
        /// Adapter logger that bridges Microsoft.Extensions.Logging.ILogger to Serilog ILogEventSink.
        /// </summary>
        private sealed class SerilogUnixSocketLogger : Microsoft.Extensions.Logging.ILogger
        {
            private readonly UnixSocketSink _sink;

            public SerilogUnixSocketLogger(UnixSocketSink sink)
            {
                _sink = sink;
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

                // Convert Microsoft.Extensions.Logging.LogLevel to Serilog.Events.LogEventLevel
                var serilogLevel = MapLogLevel(logLevel);

                // Create a Serilog LogEvent
                var logEvent = new LogEvent(
                    DateTimeOffset.UtcNow,
                    serilogLevel,
                    exception,
                    new MessageTemplateParser().Parse(message),
                    Array.Empty<LogEventProperty>());

                // Emit to the sink
                _sink.Emit(logEvent);
            }

            private static LogEventLevel MapLogLevel(LogLevel logLevel)
            {
                return logLevel switch
                {
                    LogLevel.Trace => LogEventLevel.Verbose,
                    LogLevel.Debug => LogEventLevel.Debug,
                    LogLevel.Information => LogEventLevel.Information,
                    LogLevel.Warning => LogEventLevel.Warning,
                    LogLevel.Error => LogEventLevel.Error,
                    LogLevel.Critical => LogEventLevel.Fatal,
                    LogLevel.None => LogEventLevel.Fatal, // Shouldn't happen due to IsEnabled check
                    _ => LogEventLevel.Information,
                };
            }
        }
    }
}
