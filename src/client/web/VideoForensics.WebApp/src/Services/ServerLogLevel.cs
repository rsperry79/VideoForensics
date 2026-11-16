using Serilog.Core;
using Serilog.Events;

using SperryDev.Logger.Services;

namespace VideoForensics.Client.WebApp.Services;

/// <summary>
/// The minimum level for what the server records: the console and the audit log files, both Serilog sinks.
/// It starts at Information, the default configured level, and is set to the saved level once the database is
/// reachable. Changing it applies at once, without a restart.
/// </summary>
public sealed class ServerLogLevel
{
    public LoggingLevelSwitch Switch { get; } = new(LogEventLevel.Information);

    /// <summary>The same level for the live log buffer, which is not a Serilog sink.</summary>
    public LogLevelSource BufferLevel { get; } = new() { Minimum = LogLevel.Information };

    /// <summary>
    /// Whether the audit files store an entry at this level. Trace and Debug are shown live (console and named pipe) at those
    /// settings, but never written to the files, because they grow the files too fast.
    /// </summary>
    public static bool StoresInFiles(LogEventLevel level)
    {
        return level >= LogEventLevel.Information;
    }

    /// <summary>Applies a configured level name such as "Warning". An unknown name keeps the current level.</summary>
    public void Apply(string? level)
    {
        (LogEventLevel, LogLevel)? mapped = level switch
        {
            "Trace" => (LogEventLevel.Verbose, LogLevel.Trace),
            "Debug" => (LogEventLevel.Debug, LogLevel.Debug),
            "Information" => (LogEventLevel.Information, LogLevel.Information),
            "Warning" => (LogEventLevel.Warning, LogLevel.Warning),
            "Error" => (LogEventLevel.Error, LogLevel.Error),
            "Critical" => (LogEventLevel.Fatal, LogLevel.Critical),
            _ => null,
        };

        if (mapped is { } value)
        {
            Switch.MinimumLevel = value.Item1;
            BufferLevel.Minimum = value.Item2;
        }
    }
}
