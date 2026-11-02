namespace VideoForensics.Utils.DbRepair.Contracts;

/// <summary>
/// Console input and output used by <see cref="ServerMaintenanceClient"/>. Lets the decision flow run in tests without a terminal.
/// </summary>
public interface IMaintenanceConsole
{
    /// <summary>True when a person can answer questions. False when stdin is redirected; every question then counts as no.</summary>
    bool IsInteractive { get; }

    /// <summary>Writes one line of status to the operator.</summary>
    void Write(string message);

    /// <summary>Asks a yes/no question. Returns true only for an answer of y or yes.</summary>
    Task<bool> ConfirmAsync(string question, CancellationToken ct);

    /// <summary>Opens a URL in the default browser. Returns false when it could not be opened; the caller then prints the URL.</summary>
    bool OpenBrowser(Uri url);
}
