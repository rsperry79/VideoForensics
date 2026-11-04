using System;
using System.Globalization;
using System.IO;

using Serilog.Core;
using Serilog.Events;

namespace VideoForensics.Core.Logging.Providers
{
    /// <summary>Console sink that writes the time in UTC, then the level tag and the message in the level's colour: Information green, Warning orange, Error and Fatal red. Debug and Verbose stay uncoloured.</summary>
    public sealed class ColoredConsoleSink : ILogEventSink
    {
        private const string Reset = "\u001b[0m";
        private const string Green = "\u001b[32m";
        private const string Orange = "\u001b[38;5;208m";
        private const string Red = "\u001b[31m";

        /// <summary>Writes one entry: UTC time, [LEVEL] and category on the first line, the message on the next, then any exception.</summary>
        public void Emit(LogEvent logEvent)
        {
            string? colour = ColourFor(logEvent.Level);
            string time = logEvent.Timestamp.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) + " UTC";
            string tag = "[" + TagFor(logEvent.Level) + "]";
            string header = logEvent.Properties.TryGetValue("SourceContext", out LogEventPropertyValue? source)
                ? time + " " + Paint(colour, tag) + " " + source.ToString().Trim('"')
                : time + " " + Paint(colour, tag);
            string message = Paint(colour, logEvent.RenderMessage());
            string exception = logEvent.Exception is null ? string.Empty : Environment.NewLine + logEvent.Exception;

            Console.Out.Write(header + Environment.NewLine + message + exception + Environment.NewLine);
        }

        /// <summary>The ANSI colour for a level, or null for Debug and Verbose, which stay uncoloured.</summary>
        public static string? ColourFor(LogEventLevel level) => level switch
        {
            LogEventLevel.Information => Green,
            LogEventLevel.Warning => Orange,
            LogEventLevel.Error or LogEventLevel.Fatal => Red,
            _ => null,
        };

        private static string TagFor(LogEventLevel level) => level switch
        {
            LogEventLevel.Verbose => "VRB",
            LogEventLevel.Debug => "DBG",
            LogEventLevel.Information => "INF",
            LogEventLevel.Warning => "WRN",
            LogEventLevel.Error => "ERR",
            _ => "FTL",
        };

        private static string Paint(string? colour, string text) => colour is null ? text : colour + text + Reset;
    }
}
