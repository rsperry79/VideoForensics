namespace VideoForensics.Utils.DbRepair.Contracts;

/// <summary>
/// Abstraction for password input prompts, allowing interactive terminal input during recovery.
/// Never logs passwords or accepts them via command-line arguments.
/// </summary>
public interface IPasswordPrompt
{
    /// <summary>
    /// Prompts for a new password with hidden input (no echo to terminal).
    /// </summary>
    Task<string> PromptPasswordAsync(CancellationToken ct);

    /// <summary>
    /// Prompts for password confirmation with hidden input.
    /// </summary>
    Task<string> PromptConfirmationAsync(CancellationToken ct);

    /// <summary>
    /// Asks for confirmation of an action; requires user to type 'y' to proceed.
    /// </summary>
    Task<bool> AskConfirmationAsync(string prompt, CancellationToken ct);
}
