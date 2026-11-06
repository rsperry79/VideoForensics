using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using VideoForensics.Core.Logging.Providers;

using Xunit;

namespace VideoForensics.Core.Logging.Tests.Providers
{
    public class UtcTimestampSinkTests
    {
        private const string Green = "\u001b[32m";
        private const string Orange = "\u001b[38;5;208m";
        private const string Red = "\u001b[31m";
        private const string Reset = "\u001b[0m";
        private const string Template = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} UTC [{Level:u3}] {Message:lj}{NewLine}";

        private sealed class CapturingSink : ILogEventSink
        {
            public List<LogEvent> Events { get; } = [];

            public void Emit(LogEvent logEvent)
            {
                Events.Add(logEvent);
            }
        }

        private static LogEvent Event(DateTimeOffset timestamp, LogEventLevel level)
        {
            MessageTemplate template = new MessageTemplateParser().Parse("hello");
            return new LogEvent(timestamp, level, null, template, Enumerable.Empty<LogEventProperty>());
        }

        [Fact]
        public void Emit_NonUtcOffset_ForwardsEventWithUtcTimestampAndSameInstant()
        {
            var captured = new CapturingSink();
            var sink = new UtcTimestampSink(captured);
            var local = new DateTimeOffset(2026, 11, 4, 22, 19, 5, TimeSpan.FromHours(5));

            sink.Emit(Event(local, LogEventLevel.Information));

            LogEvent forwarded = Assert.Single(captured.Events);
            Assert.Equal(TimeSpan.Zero, forwarded.Timestamp.Offset);
            Assert.Equal(local.UtcDateTime, forwarded.Timestamp.UtcDateTime);
        }

        [Theory]
        [InlineData(LogEventLevel.Information, Green)]
        [InlineData(LogEventLevel.Warning, Orange)]
        [InlineData(LogEventLevel.Error, Red)]
        [InlineData(LogEventLevel.Fatal, Red)]
        public void ColourFor_Level_ReturnsItsColour(LogEventLevel level, string expected)
        {
            Assert.Equal(expected, ColoredConsoleSink.ColourFor(level));
        }

        [Fact]
        public void ColourFor_DebugAndVerbose_ReturnsNull()
        {
            Assert.Null(ColoredConsoleSink.ColourFor(LogEventLevel.Debug));
            Assert.Null(ColoredConsoleSink.ColourFor(LogEventLevel.Verbose));
        }

        [Theory]
        [InlineData(LogEventLevel.Information, Green, "[INF]")]
        [InlineData(LogEventLevel.Warning, Orange, "[WRN]")]
        [InlineData(LogEventLevel.Error, Red, "[ERR]")]
        public void Emit_Entry_ColoursOnlyTheLevelTagAndMessage(LogEventLevel level, string colour, string tag)
        {
            TextWriter original = Console.Out;
            var captured = new StringWriter();
            try
            {
                Console.SetOut(captured);
                new ColoredConsoleSink().Emit(Event(new DateTimeOffset(2026, 11, 4, 17, 19, 5, TimeSpan.Zero), level));
            }
            finally
            {
                Console.SetOut(original);
            }

            string text = captured.ToString();
            Assert.StartsWith("2026-11-04 17:19:05.000 UTC " + colour + tag + Reset, text);
            Assert.Contains(colour + "hello" + Reset, text);
            Assert.DoesNotContain(colour + "2026", text);
        }
    }
}
