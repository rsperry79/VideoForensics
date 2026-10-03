using Microsoft.AspNetCore.Identity;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.DbRepair.Contracts;

namespace VideoForensics.DbRepair;

/// <summary>
/// Recovers SuperAdmin access when a passkey is lost.
/// Handles target selection, password validation, hashing, and account re-activation.
/// </summary>
public class SuperAdminRecovery : ISuperAdminRecovery
{
    private const int MinPasswordLength = 12;
    private readonly IOperatorRepository _operatorRepo;
    private readonly IAppSettingRepository _appSettingRepo;
    private readonly IPasswordPrompt _passwordPrompt;
    private readonly ISecurityAuditLogRepository _auditRepo;

    public SuperAdminRecovery(
        IOperatorRepository operatorRepo,
        IAppSettingRepository appSettingRepo,
        IPasswordPrompt passwordPrompt,
        ISecurityAuditLogRepository auditRepo)
    {
        _operatorRepo = operatorRepo;
        _appSettingRepo = appSettingRepo;
        _passwordPrompt = passwordPrompt;
        _auditRepo = auditRepo;
    }

    public async Task<Operator> SelectTargetAsync(string? username, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(username))
        {
            var op = await _operatorRepo.GetByUsernameAsync(username, ct)
                ?? throw new InvalidOperationException($"Operator '{username}' not found.");

            if (op.Role != OperatorRole.SuperAdmin)
            {
                throw new InvalidOperationException($"Operator '{username}' is not a SuperAdmin (role: {op.Role}).");
            }

            return op;
        }

        // No username provided: select the target automatically
        var allOps = await _operatorRepo.ListAsync(ct);
        var superAdmins = allOps.Where(o => o.Role == OperatorRole.SuperAdmin).ToList();

        if (superAdmins.Count == 0)
        {
            throw new InvalidOperationException("No SuperAdmin operators found in the database.");
        }

        // Prefer the primary SuperAdmin if it exists
        var primary = superAdmins.FirstOrDefault(o => o.IsPrimarySuperAdmin);
        if (primary is not null)
        {
            return primary;
        }

        // If there's exactly one non-primary SuperAdmin, use it
        if (superAdmins.Count == 1)
        {
            return superAdmins[0];
        }

        // Multiple SuperAdmins without a primary - ambiguous
        throw new InvalidOperationException(
            $"Multiple SuperAdmin operators found. Use --username to specify which one to recover:\n" +
            string.Join("\n", superAdmins.Select(o => $"  {o.Username} (ID: {o.Id})")));
    }

    public void ValidatePassword(string password)
    {
        if (password.Length < MinPasswordLength)
        {
            throw new InvalidOperationException($"Password must be at least {MinPasswordLength} characters.");
        }
    }

    public async Task<string> PromptPasswordAsync(CancellationToken ct)
    {
        string password = await _passwordPrompt.PromptPasswordAsync(ct);
        ValidatePassword(password);

        string confirmation = await _passwordPrompt.PromptConfirmationAsync(ct);
        if (password != confirmation)
        {
            throw new InvalidOperationException("Passwords do not match.");
        }

        return password;
    }

    public async Task ResetPasswordAsync(Operator target, string newPassword, CancellationToken ct)
    {
        ValidatePassword(newPassword);

        // Hash the new password using the same hasher as the server
        var hasher = new PasswordHasher<Operator>();
        string passwordHash = hasher.HashPassword(target, newPassword);

        // Update password (which also rotates the security stamp to invalidate existing sessions)
        await _operatorRepo.SetPasswordAsync(target.Id, passwordHash, mustChangePassword: false, ct);

        // Re-activate the account if it was deactivated
        if (!target.Active)
        {
            await _operatorRepo.ReactivateAsync(target.Id, ct);
        }

        // Approve the account if not already approved
        if (!target.IsApproved)
        {
            await _operatorRepo.ApproveAsync(target.Id, ct);
        }

        // Clear any lockout
        await _operatorRepo.UnlockAsync(target.Id, ct);

        // Enable password auth if the setting exists and is currently False
        string? currentSetting = await _appSettingRepo.GetAsync("AuthPasswordEnabled", ct);
        if (currentSetting is not null && currentSetting.Equals("False", StringComparison.OrdinalIgnoreCase))
        {
            await _appSettingRepo.SetAsync("AuthPasswordEnabled", "True", ct);
        }

        // Log the recovery event
        var auditEntry = new SecurityAuditLogEntry
        {
            Id = Guid.NewGuid(),
            TimestampUtc = DateTime.UtcNow,
            EventType = SecurityAuditEventTypes.SuperAdminPasswordReset,
            OperatorId = target.Id,
            SourceIp = "local-console",
            IsUrgent = true,
            Details = $"SuperAdmin password reset for {target.Username}"
        };
        await _auditRepo.AppendAsync(auditEntry, ct);
    }

    public async Task<Operator> ExecuteAsync(string? username, bool skipConfirmation, CancellationToken ct)
    {
        // Step 1: Select target
        var target = await SelectTargetAsync(username, ct);
        Console.WriteLine($"\nTarget SuperAdmin: {target.Username} (ID: {target.Id})");

        // Step 2: Prompt for new password
        Console.WriteLine("\nEnter new password (min 12 characters):");
        string newPassword = await PromptPasswordAsync(ct);

        // Step 3: Ask for confirmation (unless --yes)
        if (!skipConfirmation)
        {
            Console.WriteLine($"\nYou are about to reset the password for SuperAdmin '{target.Username}'.");
            Console.WriteLine("This will invalidate all existing sessions.");
            bool confirmed = await _passwordPrompt.AskConfirmationAsync(
                $"Reset password for {target.Username}? (y/n): ", ct);

            if (!confirmed)
            {
                throw new OperationCanceledException("Recovery cancelled by user.");
            }
        }

        // Step 4: Apply the reset
        await ResetPasswordAsync(target, newPassword, ct);
        Console.WriteLine($"\nPassword reset successfully for {target.Username}.");
        Console.WriteLine("Account has been re-activated, approved, and unlocked.");
        Console.WriteLine("Existing sessions have been invalidated.");

        return target;
    }
}
