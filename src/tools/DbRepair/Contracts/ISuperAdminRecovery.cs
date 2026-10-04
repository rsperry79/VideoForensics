using VideoForensics.Data.Common.Entities;

namespace VideoForensics.DbRepair.Contracts;

/// <summary>
/// Handles recovery of a SuperAdmin's access when their passkey is lost.
/// Manages target selection, password validation, hashing, and account re-activation.
/// </summary>
public interface ISuperAdminRecovery
{
    /// <summary>
    /// Selects the target SuperAdmin account to reset.
    /// If <paramref name="username"/> is provided, looks up that operator and validates it is a SuperAdmin.
    /// If null, returns the single primary SuperAdmin (error if multiple or none exist).
    /// </summary>
    Task<Operator> SelectTargetAsync(string? username, CancellationToken ct);

    /// <summary>
    /// Validates that a password meets minimum length requirements (12 characters).
    /// Throws InvalidOperationException if too short.
    /// </summary>
    void ValidatePassword(string password);

    /// <summary>
    /// Prompts interactively for a new password and confirmation.
    /// Returns the password if both match; throws if they do not or validation fails.
    /// Never accepts password via command-line or logs it.
    /// </summary>
    Task<string> PromptPasswordAsync(CancellationToken ct);

    /// <summary>
    /// Resets the SuperAdmin's password hash, unlocks the account, and re-activates if needed.
    /// Updates the security stamp to invalidate existing sessions.
    /// Enables password auth in app settings if the key exists and is currently False.
    /// Logs the recovery event to the audit trail.
    /// </summary>
    Task ResetPasswordAsync(Operator target, string newPassword, CancellationToken ct);

    /// <summary>
    /// Executes the full recovery flow: selects target, prompts for password, optionally confirms, and resets.
    /// Returns the recovered operator.
    /// If <paramref name="skipConfirmation"/> is false, asks for typed confirmation before making changes.
    /// Throws InvalidOperationException for validation errors or OperationCanceledException if user declines confirmation.
    /// </summary>
    Task<Operator> ExecuteAsync(string? username, bool skipConfirmation, CancellationToken ct);
}
