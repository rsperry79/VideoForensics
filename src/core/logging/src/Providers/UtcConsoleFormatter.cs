using System.IO;
using System.Linq;

using Serilog.Events;
using Serilog.Formatting;
using Serilog.Formatting.Display;

namespace VideoForensics.Core.Logging.Providers
{
    /// <summary>Text formatter that writes each timestamp in UTC. A Serilog output template only prints the event's own offset, so the instant is converted before the template runs.</summary>
    public sealed class UtcConsoleFormatter : ITextFormatter
    {
        private readonly MessageTemplateTextFormatter _inner;

        /// <summary>Creates the formatter. The template should print the timestamp without an offset and label it UTC.</summary>
        public UtcConsoleFormatter(string outputTemplate)
        {
            _inner = new MessageTemplateTextFormatter(outputTemplate);
        }

        /// <inheritdoc />
        public void Format(LogEvent logEvent, TextWriter output)
        {
            var utcEvent = new LogEvent(
                logEvent.Timestamp.ToUniversalTime(),
                logEvent.Level,
                logEvent.Exception,
                logEvent.MessageTemplate,
                logEvent.Properties.Select(p => new LogEventProperty(p.Key, p.Value)));

            _inner.Format(utcEvent, output);
        }
    }
}
