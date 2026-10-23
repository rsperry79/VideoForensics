using Xunit;

namespace VideoForensics.Client.Platform.Windows.Tests
{
    public class WindowsFirewallRuleManagerTests
    {
        [Fact]
        public void BuildShowRuleArguments_QuotesRuleName()
        {
            string args = WindowsFirewallRuleManager.BuildShowRuleArguments("VideoForensics Server");

            Assert.Equal("advfirewall firewall show rule name=\"VideoForensics Server\"", args);
        }

        [Fact]
        public void BuildAddRuleArguments_MatchesInstallerRuleShape()
        {
            string args = WindowsFirewallRuleManager.BuildAddRuleArguments("VideoForensics Server", 8443);

            // Same shape the installer creates, so runtime-added and install-time rules are interchangeable.
            Assert.Equal(
                "advfirewall firewall add rule name=\"VideoForensics Server\" dir=in action=allow protocol=TCP localport=8443 profile=private,domain",
                args);
        }

        [Fact]
        public void BuildDeleteRuleArguments_QuotesRuleName()
        {
            string args = WindowsFirewallRuleManager.BuildDeleteRuleArguments("VideoForensics Server");

            Assert.Equal("advfirewall firewall delete rule name=\"VideoForensics Server\"", args);
        }
    }
}
