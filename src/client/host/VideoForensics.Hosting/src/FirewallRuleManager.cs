using CliWrap;
using CliWrap.Buffered;

using Microsoft.Extensions.Logging;

namespace VideoForensics.Hosting
{
    /// <summary>
    /// Manages the Windows Firewall inbound rule that lets other devices reach this server when the
    /// configured network tier is widened beyond Local (plan §5.2) - separate from Kestrel's own
    /// bind decision (Program.cs), which only controls whether the SOCKET listens on every
    /// interface, not whether Windows actually lets inbound traffic through to it. The installer
    /// (VideoForensics.iss) only creates this rule at fresh-install time if "local network" was
    /// chosen then; widening the tier later via Settings > Network Access needs this to add the
    /// rule itself, or remote devices see a socket that's listening but firewalled - the exact bug
    /// this interface exists to close.
    /// </summary>
    public interface IFirewallRuleManager
    {
        /// <summary>Adds the named inbound TCP allow rule for the given port if it doesn't already exist. No-op if it's already present.</summary>
        Task EnsureRuleExistsAsync(string ruleName, int port, CancellationToken ct);

        /// <summary>Removes the named inbound rule if present. No-op if it doesn't exist.</summary>
        Task RemoveRuleAsync(string ruleName, CancellationToken ct);
    }

    /// <summary>
    /// Real Windows Firewall implementation via netsh.exe (CliWrap), matching the exact rule shape
    /// the installer itself creates (name, direction, protocol, profile) so runtime-added and
    /// install-time-added rules are indistinguishable.
    /// </summary>
    public class WindowsFirewallRuleManager : IFirewallRuleManager
    {
        private readonly ILogger<WindowsFirewallRuleManager> _logger;

        public WindowsFirewallRuleManager(ILogger<WindowsFirewallRuleManager> logger)
        {
            _logger = logger;
        }

        public async Task EnsureRuleExistsAsync(string ruleName, int port, CancellationToken ct)
        {
            try
            {
                // First check if the rule already exists via `netsh advfirewall firewall show rule name="<ruleName>"`.
                // It exits non-zero / prints "No rules match the specified criteria." when absent, exits 0 when present.
                BufferedCommandResult checkResult = await Cli.Wrap("netsh.exe")
                    .WithArguments($"advfirewall firewall show rule name=\"{ruleName}\"")
                    .WithValidation(CommandResultValidation.None)
                    .ExecuteBufferedAsync(ct);

                if (checkResult.ExitCode == 0)
                {
                    // Rule already exists, nothing to do.
                    _logger.LogInformation("Windows Firewall rule '{RuleName}' already exists", ruleName);
                    return;
                }

                // Rule doesn't exist, add it: `netsh advfirewall firewall add rule name="<ruleName>" dir=in action=allow protocol=TCP localport=<port> profile=private,domain`
                BufferedCommandResult addResult = await Cli.Wrap("netsh.exe")
                    .WithArguments($"advfirewall firewall add rule name=\"{ruleName}\" dir=in action=allow protocol=TCP localport={port} profile=private,domain")
                    .WithValidation(CommandResultValidation.None)
                    .ExecuteBufferedAsync(ct);

                if (addResult.ExitCode == 0)
                {
                    _logger.LogInformation("Windows Firewall rule '{RuleName}' (port {Port}) added successfully", ruleName, port);
                }
                else
                {
                    _logger.LogError("Failed to add Windows Firewall rule '{RuleName}': netsh exited with code {ExitCode}. Output: {Output}", ruleName, addResult.ExitCode, addResult.StandardError);
                }
            }
            catch (Exception ex)
            {
                // Never throw - this is best-effort defense-in-depth. The network tier change itself
                // already succeeded and the tier is now live in Kestrel; a failure to add the firewall
                // rule is not a reason to undo the entire configuration change or crash the request.
                // Log the error so the admin can see what happened and manually add the rule if needed.
                _logger.LogError(ex, "Exception while ensuring Windows Firewall rule '{RuleName}' exists", ruleName);
            }
        }

        public async Task RemoveRuleAsync(string ruleName, CancellationToken ct)
        {
            try
            {
                // `netsh advfirewall firewall delete rule name="<ruleName>"` - safe to call even if the
                // rule doesn't exist. The command exits 0 in both cases (rule deleted, or rule didn't exist).
                BufferedCommandResult result = await Cli.Wrap("netsh.exe")
                    .WithArguments($"advfirewall firewall delete rule name=\"{ruleName}\"")
                    .WithValidation(CommandResultValidation.None)
                    .ExecuteBufferedAsync(ct);

                if (result.ExitCode == 0)
                {
                    _logger.LogInformation("Windows Firewall rule '{RuleName}' removed", ruleName);
                }
                else
                {
                    _logger.LogError("Failed to remove Windows Firewall rule '{RuleName}': netsh exited with code {ExitCode}. Output: {Output}", ruleName, result.ExitCode, result.StandardError);
                }
            }
            catch (Exception ex)
            {
                // Never throw - same best-effort reasoning as EnsureRuleExistsAsync above.
                _logger.LogError(ex, "Exception while removing Windows Firewall rule '{RuleName}'", ruleName);
            }
        }
    }

    /// <summary>
    /// No-op implementation for non-Windows hosts (Linux) - Windows Firewall / netsh doesn't apply
    /// there, and this repo's Debian packaging doesn't manage a firewall today either, so doing
    /// nothing here is not a regression.
    /// </summary>
    public class NullFirewallRuleManager : IFirewallRuleManager
    {
        public Task EnsureRuleExistsAsync(string ruleName, int port, CancellationToken ct) => Task.CompletedTask;

        public Task RemoveRuleAsync(string ruleName, CancellationToken ct) => Task.CompletedTask;
    }
}
