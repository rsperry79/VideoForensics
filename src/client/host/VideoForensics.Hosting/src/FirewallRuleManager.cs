using VideoForensics.Client.Common.Contracts;

namespace VideoForensics.Hosting
{
    /// <summary>
    /// No-op implementation for hosts where the OS firewall is not managed by this app (for example Linux, where
    /// this repo's Debian packaging does not manage a firewall). The Windows implementation lives in
    /// <c>VideoForensics.Client.Platform.Windows</c> and is registered by the WebApp head on Windows.
    /// </summary>
    public class NullFirewallRuleManager : IFirewallRuleManager
    {
        public Task EnsureRuleExistsAsync(string ruleName, int port, CancellationToken ct) => Task.CompletedTask;

        public Task RemoveRuleAsync(string ruleName, CancellationToken ct) => Task.CompletedTask;
    }
}
