using VideoForensics.Providers.Ring.Services;
using Xunit;

namespace VideoForensics.Providers.Ring.Tests
{
    /// <summary>
    /// Tests for SessionProvider implementation.
    /// Verifies both the keyed (multi-account) overloads and the back-compat parameterless
    /// overloads that operate on "whichever account was most recently set".
    /// </summary>
    public class SessionProviderTests
    {
        [Fact]
        public void KeyedSetGetClear_TwoAccounts_DoNotInterfereWithEachOther()
        {
            var provider = new SessionProvider();
            var accountA = Guid.NewGuid();
            var accountB = Guid.NewGuid();
            var sessionA = new Session("a@example.com", "passwordA");
            var sessionB = new Session("b@example.com", "passwordB");

            provider.SetSession(accountA, sessionA);
            provider.SetSession(accountB, sessionB);

            Assert.Same(sessionA, provider.GetSession(accountA));
            Assert.Same(sessionB, provider.GetSession(accountB));

            provider.ClearSession(accountA);

            Assert.Null(provider.GetSession(accountA));
            Assert.Same(sessionB, provider.GetSession(accountB));
        }

        [Fact]
        public void GetSession_ForUnknownAccount_ReturnsNull()
        {
            var provider = new SessionProvider();

            Assert.Null(provider.GetSession(Guid.NewGuid()));
        }

        [Fact]
        public void ParameterlessSetGetClear_RoundTripsCorrectly()
        {
            var provider = new SessionProvider();
            var session = new Session("user@example.com", "password");

            Assert.Null(provider.GetSession());

            provider.SetSession(session);

            Assert.Same(session, provider.GetSession());

            provider.ClearSession();

            Assert.Null(provider.GetSession());
        }

        [Fact]
        public void SetSession_ViaKeyedOverload_ThenParameterlessGetSession_ReturnsSameSession()
        {
            var provider = new SessionProvider();
            var accountId = Guid.NewGuid();
            var session = new Session("user@example.com", "password");

            provider.SetSession(accountId, session);

            Assert.Same(session, provider.GetSession());
            Assert.Same(session, provider.GetSession(accountId));
        }

        [Fact]
        public void SetSession_Keyed_Null_ThrowsArgumentNullException()
        {
            var provider = new SessionProvider();

            Assert.Throws<ArgumentNullException>(() => provider.SetSession(Guid.NewGuid(), null!));
        }

        [Fact]
        public void SetSession_Parameterless_Null_ThrowsArgumentNullException()
        {
            var provider = new SessionProvider();

            Assert.Throws<ArgumentNullException>(() => provider.SetSession((Session)null!));
        }

        [Fact]
        public void ClearSession_ParameterlessAfterKeyedSet_ClearsLastSetAccountOnly()
        {
            var provider = new SessionProvider();
            var accountA = Guid.NewGuid();
            var accountB = Guid.NewGuid();
            var sessionA = new Session("a@example.com", "passwordA");
            var sessionB = new Session("b@example.com", "passwordB");

            provider.SetSession(accountA, sessionA);
            provider.SetSession(accountB, sessionB);

            // accountB was set most recently
            provider.ClearSession();

            Assert.Null(provider.GetSession(accountB));
            Assert.Null(provider.GetSession());
            Assert.Same(sessionA, provider.GetSession(accountA));
        }
    }
}
