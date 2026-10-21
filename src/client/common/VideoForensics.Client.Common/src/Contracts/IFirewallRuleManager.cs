namespace VideoForensics.Client.Common.Contracts
{
    /// <summary>
    /// Manages the inbound firewall rule that lets other devices reach this server when the configured network
    /// tier is widened beyond Local. Kestrel's bind decision only controls whether the socket listens on every
    /// interface; this controls whether the OS lets inbound traffic through to it.
    /// Windows implements it through netsh (<c>VideoForensics.Client.Platform.Windows</c>); other platforms use a no-op.
    /// </summary>
    public interface IFirewallRuleManager
    {
        /// <summary>Adds the named inbound TCP allow rule for the given port if it doesn't already exist. No-op if it's already present.</summary>
        Task EnsureRuleExistsAsync(string ruleName, int port, CancellationToken ct);

        /// <summary>Removes the named inbound rule if present. No-op if it doesn't exist.</summary>
        Task RemoveRuleAsync(string ruleName, CancellationToken ct);
    }
}
