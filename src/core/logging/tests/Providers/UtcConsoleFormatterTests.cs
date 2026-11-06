using Serilog.Events;
using Serilog.Parsing;

using System;
using System.IO;

using VideoForensics.Core.Logging.Providers;

using Xunit;

namespace VideoForensics.Core.Logging.Tests.Providers
{
    public class UtcConsoleFormatterTests
    {
        private const string Template = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} UTC [{Level:u3}] {Message:lj}{NewLine}";

        private static LogEvent Event(DateTimeOffset timestamp)
        {
            MessageTemplate template = new MessageTemplateParser().Parse("hello");
            return new LogEvent(timestamp, LogEventLevel.Information, null, template, Array.Empty<LogEventProperty>());
        }

        [Fact]
        public void Format_NonUtcOffset_WritesTimeConvertedToUtc()
        {
            var local = new DateTimeOffset(2026, 11, 4, 22, 19, 5, TimeSpan.FromHours(5));
            var output = new StringWriter();

            new UtcConsoleFormatter(Template).Format(Event(local), output);

            Assert.StartsWith("2026-11-04 17:19:05.000 UTC [INF] hello", output.ToString());
        }

        [Fact]
        public void Format_UtcEvent_KeepsTheSameInstant()
        {
            var utc = new DateTimeOffset(2026, 11, 4, 17, 19, 5, TimeSpan.Zero);
            var output = new StringWriter();

            new UtcConsoleFormatter(Template).Format(Event(utc), output);

            Assert.StartsWith("2026-11-04 17:19:05.000 UTC [INF] hello", output.ToString());
        }
    }
}
