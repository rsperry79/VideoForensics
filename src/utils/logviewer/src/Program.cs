using System.IO.Pipes;
using System.Text.Json;

// VideoForensics.Utils.LogViewer: displays the VideoForensics named pipe logger output in this console.
//
// The logger (NamedPipeLoggerProvider) is the pipe server: it creates "VideoForensics.Logger" as a
// single-instance outbound pipe and writes one JSON object per line while a client is connected.
// This app is the client: it connects, prints each entry, and reconnects when the server goes away.
//
// Usage:
//   VideoForensics.Utils.LogViewer [--pipe <name>] [--raw] [--help]
//
// --pipe  pipe name to connect to (default: VideoForensics.Logger)
// --raw   print each line as received instead of formatting the JSON

const string DefaultPipeName = "VideoForensics.Logger";

string pipeName = DefaultPipeName;
bool raw = false;

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--pipe" when i + 1 < args.Length:
            pipeName = args[++i];
            break;
        case "--raw":
            raw = true;
            break;
        case "--help":
        case "-h":
            Console.WriteLine("Usage: VideoForensics.Utils.LogViewer [--pipe <name>] [--raw] [--help]");
            return 0;
        default:
            Console.Error.WriteLine($"Unknown argument: {args[i]}");
            return 1;
    }
}

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

Console.WriteLine($"Waiting for logger on pipe '{pipeName}'. Press Ctrl+C to exit.");

while (!cts.IsCancellationRequested)
{
    try
    {
        // Connect with a timeout so the loop can notice cancellation while the server is absent.
        await using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.In, PipeOptions.Asynchronous);
        await client.ConnectAsync(1000, cts.Token);
        Console.WriteLine($"Connected to '{pipeName}'.");

        using var reader = new StreamReader(client);
        while (await reader.ReadLineAsync(cts.Token) is { } line)
        {
            Console.WriteLine(raw ? line : FormatLine(line));
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

return 0;

// Formats a logger JSON line as "timestamp level [category] message"; falls back to the raw line.
static string FormatLine(string line)
{
    try
    {
        using JsonDocument doc = JsonDocument.Parse(line);
        JsonElement root = doc.RootElement;
        string timestamp = root.TryGetProperty("timestamp", out JsonElement ts) ? ts.GetString() ?? string.Empty : string.Empty;
        string level = root.TryGetProperty("level", out JsonElement lv) ? lv.GetString() ?? string.Empty : string.Empty;
        string category = root.TryGetProperty("category", out JsonElement cat) ? cat.GetString() ?? string.Empty : string.Empty;
        string message = root.TryGetProperty("message", out JsonElement msg) ? msg.GetString() ?? string.Empty : string.Empty;
        return $"{timestamp} {level,-11} [{category}] {message}";
    }
    catch (JsonException)
    {
        return line;
    }
}
