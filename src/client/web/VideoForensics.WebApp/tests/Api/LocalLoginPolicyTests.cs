using VideoForensics.Client.WebApp.Api;

using Xunit;

namespace VideoForensics.Client.WebApp.Tests.Api
{
    /// <summary>
    /// What a password sign-in does on the local network depends on the operator's passkey setting. Over the tunnel, the normal
    /// two-factor flow always applies.
    /// </summary>
    public class LocalLoginPolicyTests
    {
        [Fact]
        public void LocalLoginPolicy_PasskeyNotRequiredOnLocalNetwork_CompletesWithPassword()
        {
            Assert.Equal(LocalPasswordOutcome.Complete, LocalLoginPolicy.Decide(passkeyRequired: false, isDirectLocal: true));
        }

        [Fact]
        public void LocalLoginPolicy_PasskeyRequiredOnLocalNetwork_GoesToAddPasskey()
        {
            Assert.Equal(LocalPasswordOutcome.AddPasskey, LocalLoginPolicy.Decide(passkeyRequired: true, isDirectLocal: true));
        }

        [Fact]
        public void LocalLoginPolicy_OverTheTunnel_FollowsTheNormalFlow()
        {
            Assert.Equal(LocalPasswordOutcome.Normal, LocalLoginPolicy.Decide(passkeyRequired: false, isDirectLocal: false));
            Assert.Equal(LocalPasswordOutcome.Normal, LocalLoginPolicy.Decide(passkeyRequired: true, isDirectLocal: false));
        }
    }
}
