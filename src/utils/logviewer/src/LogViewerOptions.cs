using CommandLine;

namespace VideoForensics.Utils.LogViewer;

/// <summary>Command-line options for the LogViewer console, parsed by CommandLineParser.</summary>
public sealed class LogViewerOptions
{
    /// <summary>Named pipe to connect to. Must match the pipe name the logger creates.</summary>
    [Option('p', "pipe", Default = "VideoForensics.Logger", HelpText = "Named pipe to connect to.")]
    public string Pipe { get; set; } = "VideoForensics.Logger";

    /// <summary>Lowest log level to show. Entries below it are dropped; lines that are not log entries always show.</summary>
    [Option('l', "level", Default = "Trace", HelpText = "Lowest level to show: Trace, Debug, Information, Warning, Error or Critical.")]
    public string Level { get; set; } = "Trace";

    /// <summary>When set, print each line as received instead of formatting the JSON and colouring by level.</summary>
    [Option("raw", HelpText = "Print each line as received instead of formatting it.")]
    public bool Raw { get; set; }
}
