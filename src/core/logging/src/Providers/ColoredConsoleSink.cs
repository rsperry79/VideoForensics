using System;
using System.IO;

using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Display;

namespace VideoForensics.Core.Logging.Providers
{
    /// <summary>Console sink that colours each whole entry by level: Information green, Warning orange, Error and above red.</summary>
    public sealed class ColoredConsoleSink : ILogEventSink
    {
        private const string Reset = "\u001b[0m";
        private const string Green = "\u001b[32m";
        private const string Orange = "\u001b[38;5;208m";
        private const string Red = "\u001b[31m";

        private readonly MessageTemplateTextFormatter _formatter;

        /// <summary>Creates the sink. The template is the same Serilog output template a console sink takes.</summary>
        public ColoredConsoleSink(string outputTemplate)
        {
            _formatter = new MessageTemplateTextFormatter(outputTemplate);
        }

        /// <inheritdoc />
        public void Emit(LogEvent logEvent)
        {
            var writer = new StringWriter();
            _formatter.Format(logEvent, writer);
            string text = writer.ToString();

            string? colour = ColourFor(logEvent.Level);
            Console.Out.Write(colour is null ? text : colour + text.TrimEnd('\r', '\n') + Reset + Environment.NewLine);
        }

        /// <summary>The ANSI colour for a level, or null for Debug and Verbose, which stay uncoloured.</summary>
        public static string? ColourFor(LogEventLevel level) => level switch
        {
            LogEventLevel.Information => Green,
            LogEventLevel.Warning => Orange,
            LogEventLevel.Error or LogEventLevel.Fatal => Red,
            _ => null,
        };
    }
}
