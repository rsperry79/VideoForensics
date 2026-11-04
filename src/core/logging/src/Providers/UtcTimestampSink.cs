using Serilog.Core;
using Serilog.Events;

namespace VideoForensics.Core.Logging.Providers
{
    /// <summary>Sink that forwards each event to an inner sink with its timestamp converted to UTC, so a template that prints the time labelled UTC is correct.</summary>
    public sealed class UtcTimestampSink : ILogEventSink
    {
        private readonly ILogEventSink _inner;

        /// <summary>Wraps an inner sink that writes the events.</summary>
        public UtcTimestampSink(ILogEventSink inner)
        {
            _inner = inner;
        }

        /// <summary>Console output with UTC time and colour by level (see <see cref="ColoredConsoleSink"/>).</summary>
        public static UtcTimestampSink Console()
        {
            return new UtcTimestampSink(new ColoredConsoleSink());
        }

        /// <inheritdoc />
        public void Emit(LogEvent logEvent)
        {
            _inner.Emit(UtcConsoleFormatter.ToUtc(logEvent));
        }
    }
}
