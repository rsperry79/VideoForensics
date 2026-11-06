using System.Diagnostics;

using VideoForensics.Utils.DbRepair.Contracts;

namespace VideoForensics.Utils.DbRepair;

/// <summary>Maintenance console backed by the terminal. Redirected input counts as non-interactive.</summary>
public sealed class ConsoleMaintenancePrompts : IMaintenanceConsole
{
    /// <inheritdoc />
    public bool IsInteractive => !Console.IsInputRedirected;

    /// <inheritdoc />
    public void Write(string message)
    {
        Console.WriteLine(message);
    }

    /// <inheritdoc />
    public Task<bool> ConfirmAsync(string question, CancellationToken ct)
    {
        Console.Write($"{question} ");
        string? answer = Console.ReadLine();
        string normalized = answer?.Trim() ?? string.Empty;
        return Task.FromResult(normalized.Equals("y", StringComparison.OrdinalIgnoreCase) || normalized.Equals("yes", StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    public bool OpenBrowser(Uri url)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(url.AbsoluteUri) { UseShellExecute = true });
            return process is not null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Write($"The browser could not be opened ({ex.GetType().Name}).");
            return false;
        }
    }
}
