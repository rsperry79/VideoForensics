using Microsoft.JSInterop;

using Moq;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting;
using VideoForensics.Ui.Shared.Services;
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

        private static async Task<(SessionTierHeaderHandler handler, RecordingInnerHandler inner)> CreateHandlerAsync(
            NetworkTier tier, string? sessionToken, ISessionTierHeaderProtector? protector = null, SessionPrincipal? principal = null)
        {
            var networkContext = new SessionNetworkContext();
            networkContext.SetTier(tier);

            var sessionState = new PairedSessionState(new Mock<IJSRuntime>().Object);
            if (sessionToken != null)
            {
                await sessionState.SetAsync(sessionToken, Guid.NewGuid(), OperatorRole.SuperAdmin.ToString());
            }

            var tokenService = new Mock<ISessionTokenService>();
            if (sessionToken != null)
            {
                _ = tokenService.Setup(t => t.Validate(sessionToken)).Returns(principal);
            }

            protector ??= new Mock<ISessionTierHeaderProtector>().Object;

            var inner = new RecordingInnerHandler { InnerHandler = new NoopHandler() };
            var handler = new SessionTierHeaderHandler(networkContext, sessionState, tokenService.Object, protector)
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
        public async Task SendAsync_ValidSessionPrincipal_AttachesProtectedHeaderWithCorrectTierAndOperator()
        {
            // Arrange
            var operatorId = Guid.NewGuid();
            var principal = new SessionPrincipal(operatorId, null, CredentialKind.Password, OperatorRole.SuperAdmin, Guid.NewGuid(), DateTime.UtcNow, DateTime.UtcNow.AddHours(12));
            var protector = new Mock<ISessionTierHeaderProtector>();
            _ = protector.Setup(p => p.Protect(NetworkTier.Internet, operatorId)).Returns("protected-value");

            (SessionTierHeaderHandler handler, RecordingInnerHandler inner) = await CreateHandlerAsync(NetworkTier.Internet, "valid-token", protector.Object, principal);
            using var invoker = new HttpMessageInvoker(handler);
            var request = new HttpRequestMessage(HttpMethod.Get, "https://localhost/api/v1/security-events");

            // Act
            _ = await invoker.SendAsync(request, CancellationToken.None);

            // Assert
            Assert.NotNull(inner.LastRequest);
            Assert.True(inner.LastRequest!.Headers.TryGetValues(SessionTierHeaderNames.HeaderName, out IEnumerable<string>? values));
            Assert.Equal("protected-value", values!.Single());
            protector.Verify(p => p.Protect(NetworkTier.Internet, operatorId), Times.Once);
        }

        [Fact]
        public async Task SendAsync_NoSessionToken_AttachesNoHeader()
        {
            // Arrange
            (SessionTierHeaderHandler handler, RecordingInnerHandler inner) = await CreateHandlerAsync(NetworkTier.Local, sessionToken: null);
            using var invoker = new HttpMessageInvoker(handler);
            var request = new HttpRequestMessage(HttpMethod.Get, "https://localhost/api/v1/security-events");

            // Act
            _ = await invoker.SendAsync(request, CancellationToken.None);

            // Assert
            Assert.NotNull(inner.LastRequest);
            Assert.False(inner.LastRequest!.Headers.Contains(SessionTierHeaderNames.HeaderName));
        }

        [Fact]
        public async Task SendAsync_SessionTokenFailsValidation_AttachesNoHeader()
        {
            // Arrange - a session token is present but no longer validates (expired/revoked) - no
            // authenticated principal to bind the header to, so no header must be sent at all.
            (SessionTierHeaderHandler handler, RecordingInnerHandler inner) = await CreateHandlerAsync(NetworkTier.Local, "stale-token", principal: null);
            using var invoker = new HttpMessageInvoker(handler);
            var request = new HttpRequestMessage(HttpMethod.Get, "https://localhost/api/v1/security-events");

            // Act
            _ = await invoker.SendAsync(request, CancellationToken.None);

            // Assert
            Assert.NotNull(inner.LastRequest);
            Assert.False(inner.LastRequest!.Headers.Contains(SessionTierHeaderNames.HeaderName));
        }
    }
}
