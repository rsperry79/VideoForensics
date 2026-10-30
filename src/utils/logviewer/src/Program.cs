using System.Globalization;
using System.IO.Pipes;
using System.Text.Json;
using CommandLine;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Parsing;
using VideoForensics.Utils.LogViewer;

// VideoForensics.Utils.LogViewer: displays the VideoForensics named pipe logger output in this console.
//
// The logger (NamedPipeLoggerProvider) is the pipe server: it creates "VideoForensics.Logger" as a
// single-instance outbound pipe and writes one JSON object per line while a client is connected.
// This app is the client: it connects, prints each entry, and reconnects when the server goes away.
//
// Usage:
//   VideoForensics.Utils.LogViewer [--pipe <name>] [--level <name>] [--raw] [--help]
//
// Run with --help for the option list. Formatted lines use the same Serilog console sink and layout as the
// WebApp, so the level colours match the server's console.

ParserResult<LogViewerOptions> parsed = Parser.Default.ParseArguments<LogViewerOptions>(args);
if (parsed.Errors.Any())
{
    // Help and version requests are not failures; the parser has already written the text.
    return parsed.Errors.Any(e => e.Tag is ErrorType.HelpRequestedError or ErrorType.VersionRequestedError) ? 0 : 1;
}

LogViewerOptions options = parsed.Value;

if (!TryParseLevel(options.Level, out LogEventLevel minimumLevel))
{
    Console.Error.WriteLine($"Unknown level '{options.Level}'. Use Trace, Debug, Information, Warning, Error or Critical.");
    return 1;
}

// Same output template as the WebApp's console sink (Program.cs there). No theme is set, so the sink's default applies.
Logger logger = new LoggerConfiguration()
    .MinimumLevel.Is(minimumLevel)
    .WriteTo.Console(outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .CreateLogger();

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

// Ctrl+D exits too. It is read as a key, not as end-of-input, so the console keeps working when input is redirected.
if (!Console.IsInputRedirected)
{
    _ = Task.Run(() =>
    {
        while (!cts.IsCancellationRequested)
        {
            ConsoleKeyInfo key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.D && key.Modifiers.HasFlag(ConsoleModifiers.Control))
            {
                cts.Cancel();
                return;
            }
        }
    });
}

// Where the server setting lives, so a user who sees no output knows what to turn on.
Console.WriteLine("The server's log pipe is off by default. To turn it on, open the WebApp and go to:");
Console.WriteLine("  Admin > Devices & Network > Infrastructure > Diagnostics");
Console.WriteLine("  tick \"Open the log pipe for LogViewer\", then press Save.");
Console.WriteLine();

Console.WriteLine($"Waiting for logger on pipe '{options.Pipe}'. Press Ctrl+D or Ctrl+C to exit.");

while (!cts.IsCancellationRequested)
{
    try
    {
        // Connect with a timeout so the loop can notice cancellation while the server is absent.
        await using var client = new NamedPipeClientStream(".", options.Pipe, PipeDirection.In, PipeOptions.Asynchronous);
        await client.ConnectAsync(1000, cts.Token);
        Console.WriteLine($"Connected to '{options.Pipe}'.");

        using var reader = new StreamReader(client);
        while (await reader.ReadLineAsync(cts.Token) is { } line)
        {
            if (!options.Raw && ToLogEvent(line) is { } logEvent)
            {
                logger.Write(logEvent);
            }
            else
            {
                Console.WriteLine(line);
            }
        }

        Console.WriteLine("Logger disconnected. Waiting for it to return...");
    }
    catch (OperationCanceledException) when (cts.IsCancellationRequested)
    {
        break;
    }
    catch (TimeoutException)
    {
        // No server yet; retry.
    }
    catch (IOException ex)
    {
        Console.Error.WriteLine($"Pipe error ({ex.GetType().Name}): {ex.Message}");
    }
    catch (Exception ex)
    {
        // Keep the console open on any other failure so the loop can reconnect.
        Console.Error.WriteLine($"Unexpected error ({ex.GetType().Name}): {ex.Message}");
    }

    try
    {
        await Task.Delay(500, cts.Token);
    }
    catch (OperationCanceledException)
    {
        break;
    }
}

await Log.CloseAndFlushAsync();
return 0;

// Turns a logger JSON line into a Serilog event with the server's timestamp, level, and category.
// Returns null when the line is not a logger entry, so the caller prints it unchanged.
static LogEvent? ToLogEvent(string line)
{
    try
    {
        using var doc = JsonDocument.Parse(line);
        JsonElement root = doc.RootElement;
        string timestamp = Text(root, "timestamp");
        string category = Text(root, "category");
        string message = Text(root, "message");
        DateTimeOffset time = DateTimeOffset.TryParse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset parsed)
            ? parsed
            : DateTimeOffset.Now;

        return new LogEvent(
            time,
            ToSerilogLevel(Text(root, "level")),
            null,
            new MessageTemplateParser().Parse("{Message}"),
            [
                new LogEventProperty("SourceContext", new ScalarValue(category)),
                new LogEventProperty("Message", new ScalarValue(message)),
            ]);
    }
    catch (JsonException)
    {
        return null;
    }
}

static string Text(JsonElement root, string property)
{
    return root.TryGetProperty(property, out JsonElement value) ? value.GetString() ?? string.Empty : string.Empty;
}

// Maps the logger's level names to Serilog's, so the sink colours each line the way the WebApp's console does.
static LogEventLevel ToSerilogLevel(string level) => level switch
{
    "Trace" => LogEventLevel.Verbose,
    "Debug" => LogEventLevel.Debug,
    "Warning" => LogEventLevel.Warning,
    "Error" => LogEventLevel.Error,
    "Critical" => LogEventLevel.Fatal,
    _ => LogEventLevel.Information,
};

// Reads the --level value, ignoring case. Returns false for a name that is not a logger level.
static bool TryParseLevel(string name, out LogEventLevel level)
{
    foreach (string known in new[] { "Trace", "Debug", "Information", "Warning", "Error", "Critical" })
    {
        if (string.Equals(known, name, StringComparison.OrdinalIgnoreCase))
        {
            level = ToSerilogLevel(known);
            return true;
        }
    }

    level = LogEventLevel.Verbose;
    return false;
}
