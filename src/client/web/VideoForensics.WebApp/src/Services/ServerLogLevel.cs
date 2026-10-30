using Microsoft.Extensions.Logging;

using Serilog.Core;
using Serilog.Events;

namespace VideoForensics.Client.WebApp.Services;

/// <summary>
/// The minimum level for what the server records: the console and the audit log files, both Serilog sinks.
/// It starts at Information, the default configured level, and is set to the saved level once the database is
/// reachable. Changing it applies at once, without a restart.
/// </summary>
public sealed class ServerLogLevel
{
    public LoggingLevelSwitch Switch { get; } = new(LogEventLevel.Information);

    /// <summary>Applies a configured level name such as "Warning". An unknown name keeps the current level.</summary>
    public void Apply(string? level)
    {
        LogEventLevel? mapped = level switch
        {
            "Trace" => LogEventLevel.Verbose,
            "Debug" => LogEventLevel.Debug,
            "Information" => LogEventLevel.Information,
            "Warning" => LogEventLevel.Warning,
            "Error" => LogEventLevel.Error,
            "Critical" => LogEventLevel.Fatal,
            _ => null,
        };

        if (mapped is { } value)
        {
            Switch.MinimumLevel = value;
        }
    }
}
