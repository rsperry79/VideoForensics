using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Providers.Ring.Core.Tests.Mocks;

namespace VideoForensics.Providers.Ring.Core.Tests
{
    /// <summary>
    /// Real Ring check for the Query API account-status payload. Restores the refresh token saved in the database
    /// (see RealSessionHelper) and asserts on the status the Query page would show. Calls Ring and may refresh the
    /// saved token, so it skips when no credentials are saved and is meant for manual runs.
    /// </summary>
    public class RealAuthStatusIntegrationTests : IAsyncLifetime
    {
        private bool _credentialsAvailable;

        public ValueTask InitializeAsync()
        {
            _credentialsAvailable = RealSessionHelper.CredentialsAvailable();
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }

        [Fact]
        [Trait("Description", "Real API: after restoring saved credentials, the status payload reports a live, unexpired token")]
        public async Task RealAuthStatus_AfterRestore_ReportsLiveUnexpiredToken()
        {
            Mocks.RealSessionHelper.RequireLive();
            if (!_credentialsAvailable)
            {
                Assert.Skip(RealSessionHelper.GetSetupInstructions());
                return;
            }

            ProviderAuthStatus? status = await RealSessionHelper.RestoreAndGetAuthStatusAsync();

            Assert.NotNull(status);
            Assert.True(status!.IsAuthenticated, "Ring restored the saved credentials but the status reports not authenticated.");
            Assert.False(string.IsNullOrWhiteSpace(status.TokenType), "Ring returned a token without a token type.");
            Assert.NotNull(status.ExpiresAtUtc);
            Assert.True(status.ExpiresAtUtc > DateTime.UtcNow, "Ring issued a token that is already expired.");
        }
    }
}
