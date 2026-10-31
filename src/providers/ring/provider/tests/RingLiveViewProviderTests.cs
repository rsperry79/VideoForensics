using Microsoft.Extensions.Logging;

using Moq;

using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Providers.Ring.Services;

using Xunit;

namespace VideoForensics.Providers.Ring.Tests
{
    /// <summary>Live view resolves the Ring session of the account that owns the camera, restoring it when it is not in memory.</summary>
    public class RingLiveViewProviderTests
    {
        private static readonly Guid AccountId = Guid.Parse("0f0f0f0f-0000-4000-8000-000000000001");

        private readonly Mock<ISessionProvider> _sessions = new();
        private readonly Mock<IProviderAuthService> _auth = new();

        private RingLiveViewProvider CreateProvider()
        {
            return new RingLiveViewProvider(_sessions.Object, _auth.Object, new Mock<ILogger<RingLiveViewProvider>>().Object);
        }

        [Fact]
        public async Task StartLiveViewAsync_NoSessionAndRestoreFails_ThrowsAfterTryingToRestoreThatAccount()
        {
            // Arrange
            _ = _sessions.Setup(s => s.GetSession(AccountId)).Returns((Session?)null);
            _ = _auth.Setup(a => a.RestoreFromSavedCredentialsAsync(AccountId, It.IsAny<CancellationToken>())).ReturnsAsync(false);

            // Act
            InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => CreateProvider().StartLiveViewAsync("1234", AccountId, CancellationToken.None));

            // Assert
            Assert.Contains("Ring", ex.Message);
            _auth.Verify(a => a.RestoreFromSavedCredentialsAsync(AccountId, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task StartLiveViewAsync_NoSessionEvenAfterRestore_ThrowsWithoutFallingBackToAnotherAccount()
        {
            // Arrange: the restore reports success but the account still has no session.
            _ = _sessions.Setup(s => s.GetSession(AccountId)).Returns((Session?)null);
            _ = _auth.Setup(a => a.RestoreFromSavedCredentialsAsync(AccountId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

            // Act
            _ = await Assert.ThrowsAsync<InvalidOperationException>(
                () => CreateProvider().StartLiveViewAsync("1234", AccountId, CancellationToken.None));

            // Assert
            _sessions.Verify(s => s.GetSession(), Times.Never);
            _sessions.Verify(s => s.GetSession(Guid.Empty), Times.Never);
        }
    }
}
