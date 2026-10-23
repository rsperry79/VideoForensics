
using CliWrap;

using CliWrap.Buffered;

using Microsoft.Extensions.Logging;

using System.Runtime.Versioning;

using VideoForensics.Client.Common.Contracts;

namespace VideoForensics.Client.Platform.Windows
{
    /// <summary>
    /// Real Windows Firewall implementation via netsh.exe (CliWrap), matching the exact rule shape
    /// the installer itself creates (name, direction, protocol, profile) so runtime-added and
    /// install-time-added rules are indistinguishable.
    /// <para>
    /// Windows Firewall manages the inbound rule that lets other devices reach this server when the configured
    /// network tier is widened beyond Local. Kestrel's bind decision only controls whether the socket listens on
    /// every interface; this rule controls whether Windows lets the inbound traffic through. Without it, remote
    /// devices see a listening socket that is firewalled.
    /// </para>
    /// </summary>
    [SupportedOSPlatform("windows")]
    public class WindowsFirewallRuleManager : IFirewallRuleManager
    {
        private readonly ILogger<WindowsFirewallRuleManager> _logger;

        public WindowsFirewallRuleManager(ILogger<WindowsFirewallRuleManager> logger)
        {
            _logger = logger;
        }

        /// <summary>Builds the <c>netsh</c> arguments that check whether a rule exists.</summary>
        public static string BuildShowRuleArguments(string ruleName)
        {
            return $"advfirewall firewall show rule name=\"{ruleName}\"";
        }

        /// <summary>
        /// Builds the <c>netsh</c> arguments that add an inbound TCP allow rule. The shape matches what the installer
        /// creates, so runtime-added and install-time-added rules are indistinguishable.
        /// </summary>
        public static string BuildAddRuleArguments(string ruleName, int port)
        {
            return $"advfirewall firewall add rule name=\"{ruleName}\" dir=in action=allow protocol=TCP localport={port} profile=private,domain";
        }

        /// <summary>Builds the <c>netsh</c> arguments that delete a rule. Safe to run when the rule is absent.</summary>
        public static string BuildDeleteRuleArguments(string ruleName)
        {
            return $"advfirewall firewall delete rule name=\"{ruleName}\"";
        }

        /// <inheritdoc />
        public async Task EnsureRuleExistsAsync(string ruleName, int port, CancellationToken ct)
        {
            try
            {
                // netsh exits 0 when the rule exists and non-zero (with "No rules match...") when it does not.
                BufferedCommandResult checkResult = await Cli.Wrap("netsh.exe")
                    .WithArguments(BuildShowRuleArguments(ruleName))
                    .WithValidation(CommandResultValidation.None)
                    .ExecuteBufferedAsync(ct)
                    .ConfigureAwait(true);

                if (checkResult.ExitCode == 0)
                {
                    _logger.LogDebug("Windows Firewall rule '{RuleName}' already exists", ruleName);
                    return;
                }

                BufferedCommandResult addResult = await Cli.Wrap("netsh.exe")
                    .WithArguments(BuildAddRuleArguments(ruleName, port))
                    .WithValidation(CommandResultValidation.None)
                    .ExecuteBufferedAsync(ct)
                    .ConfigureAwait(true);

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
                // Never throw. This is best-effort defense in depth: the network tier change has already
                // taken effect in Kestrel, so a firewall failure must not undo it or crash the request.
                _logger.LogError(ex, "Exception while ensuring Windows Firewall rule '{RuleName}' exists", ruleName);
            }
        }

        /// <inheritdoc />
        public async Task RemoveRuleAsync(string ruleName, CancellationToken ct)
        {
            try
            {
                // Exits 0 whether the rule was deleted or never existed.
                BufferedCommandResult result = await Cli.Wrap("netsh.exe")
                    .WithArguments(BuildDeleteRuleArguments(ruleName))
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
                // Never throw - same best-effort reasoning as EnsureRuleExistsAsync.
                _logger.LogError(ex, "Exception while removing Windows Firewall rule '{RuleName}'", ruleName);
            }
        }
    }
}
