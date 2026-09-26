using Moq;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting;
using VideoForensics.WebApp.Services;

using Xunit;

namespace VideoForensics.WebApp.Tests
{
    /// <summary>
    /// SessionTierHeaderHandler is the outgoing half of the session-tier-header fix: it attaches
    /// the circuit's real, captured network tier (SessionNetworkContext) to every self-HTTP call the
    /// WebApp's own Blazor UI makes back into its own Minimal API, so
    /// PairedDeviceAuthenticationHandlerTests' session-tier-header tests have something real to
    /// validate server-side. See SelfHttpServiceExtensions for where this is wired into the
    /// self-HTTP client pipeline.
    /// </summary>
    public class SessionTierHeaderHandlerTests
    {
        private sealed class RecordingInnerHandler : DelegatingHandler
        {
            public HttpRequestMessage? LastRequest { get; private set; }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                LastRequest = request;
                return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
            }
        }

        private static (SessionTierHeaderHandler handler, RecordingInnerHandler inner) CreateHandler(
            NetworkTier tier,
            string? bearerToken,
            bool attachPreAuthHeaderWhenNoToken,
            ISessionTierHeaderProtector? protector = null,
            SessionPrincipal? principal = null)
        {
            var networkContext = new SessionNetworkContext();
            networkContext.SetTier(tier);

            var tokenService = new Mock<ISessionTokenService>();
            if (bearerToken != null)
            {
                _ = tokenService.Setup(t => t.Validate(bearerToken)).Returns(principal);
            }

            protector ??= new Mock<ISessionTierHeaderProtector>().Object;

            var inner = new RecordingInnerHandler { InnerHandler = new NoopHandler() };
            var handler = new SessionTierHeaderHandler(networkContext, bearerToken, attachPreAuthHeaderWhenNoToken, tokenService.Object, protector)
            {
                InnerHandler = inner
            };
            return (handler, inner);
        }

        private sealed class NoopHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
            }
        }

        [Fact]
        public async Task SendAsync_ValidBearerToken_AttachesOperatorBoundProtectedHeader()
        {
            // Arrange
            var operatorId = Guid.NewGuid();
            var principal = new SessionPrincipal(operatorId, null, CredentialKind.Password, OperatorRole.SuperAdmin, Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow.AddHours(12));
            var protector = new Mock<ISessionTierHeaderProtector>();
            _ = protector.Setup(p => p.Protect(NetworkTier.Internet, operatorId)).Returns("protected-value");

            (SessionTierHeaderHandler handler, RecordingInnerHandler inner) = CreateHandler(
                NetworkTier.Internet, "valid-token", attachPreAuthHeaderWhenNoToken: false, protector.Object, principal);
            using var invoker = new HttpMessageInvoker(handler);
            var request = new HttpRequestMessage(HttpMethod.Get, "https://localhost/api/v1/security-events");

            // Act
            _ = await invoker.SendAsync(request, CancellationToken.None);

            // Assert
            Assert.NotNull(inner.LastRequest);
            Assert.True(inner.LastRequest!.Headers.TryGetValues(SessionTierHeaderNames.HeaderName, out IEnumerable<string>? values));
            Assert.Equal("protected-value", values!.Single());
            protector.Verify(p => p.Protect(NetworkTier.Internet, operatorId), Times.Once);
            protector.Verify(p => p.ProtectPreAuth(It.IsAny<NetworkTier>()), Times.Never);
        }

        [Fact]
        public async Task SendAsync_NoBearerToken_PreAuthAttachmentOff_AttachesNoHeader()
        {
            // Arrange - the existing (already-authenticated) self-call use: no session simply means
            // "not signed in", not "pre-auth" - unchanged from before pre-auth headers existed.
            (SessionTierHeaderHandler handler, RecordingInnerHandler inner) = CreateHandler(
                NetworkTier.Local, bearerToken: null, attachPreAuthHeaderWhenNoToken: false);
            using var invoker = new HttpMessageInvoker(handler);
            var request = new HttpRequestMessage(HttpMethod.Get, "https://localhost/api/v1/security-events");

            // Act
            _ = await invoker.SendAsync(request, CancellationToken.None);

            // Assert
            Assert.NotNull(inner.LastRequest);
            Assert.False(inner.LastRequest!.Headers.Contains(SessionTierHeaderNames.HeaderName));
        }

        [Fact]
        public async Task SendAsync_BearerTokenFailsValidation_AttachesNoHeader()
        {
            // Arrange - a bearer token is present but no longer validates (expired/revoked) - no
            // authenticated principal to bind the header to, so no header must be sent at all (even
            // with pre-auth attachment on: a stale/invalid token is not the same as no token at all).
            (SessionTierHeaderHandler handler, RecordingInnerHandler inner) = CreateHandler(
                NetworkTier.Local, "stale-token", attachPreAuthHeaderWhenNoToken: true, principal: null);
            using var invoker = new HttpMessageInvoker(handler);
            var request = new HttpRequestMessage(HttpMethod.Get, "https://localhost/api/v1/security-events");

            // Act
            _ = await invoker.SendAsync(request, CancellationToken.None);

            // Assert
            Assert.NotNull(inner.LastRequest);
            Assert.False(inner.LastRequest!.Headers.Contains(SessionTierHeaderNames.HeaderName));
        }

        [Fact]
        public async Task SendAsync_NoBearerToken_PreAuthAttachmentOn_AttachesPreAuthHeader()
        {
            // Arrange - WebAuthnClient's pre-auth calls (login, first-run setup, pairing) have no
            // session/operator identity yet, but the caller (CreateSelfHttpClientWithBearerToken) opts
            // into attaching a pre-auth header anyway, so a pre-auth endpoint like
            // OperatorAuthEndpoints.LoginPasswordAsync can still recover the real tier.
            var protector = new Mock<ISessionTierHeaderProtector>();
            _ = protector.Setup(p => p.ProtectPreAuth(NetworkTier.Internet)).Returns("pre-auth-header-value");

            (SessionTierHeaderHandler handler, RecordingInnerHandler inner) = CreateHandler(
                NetworkTier.Internet, bearerToken: null, attachPreAuthHeaderWhenNoToken: true, protector.Object);
            using var invoker = new HttpMessageInvoker(handler);
            var request = new HttpRequestMessage(HttpMethod.Post, "https://localhost/api/v1/auth/login/password");

            // Act
            _ = await invoker.SendAsync(request, CancellationToken.None);

            // Assert
            Assert.NotNull(inner.LastRequest);
            Assert.True(inner.LastRequest!.Headers.TryGetValues(SessionTierHeaderNames.HeaderName, out IEnumerable<string>? values));
            Assert.Equal("pre-auth-header-value", values!.Single());
            protector.Verify(p => p.ProtectPreAuth(NetworkTier.Internet), Times.Once);
            protector.Verify(p => p.Protect(It.IsAny<NetworkTier>(), It.IsAny<Guid>()), Times.Never);
        }
    }
}
