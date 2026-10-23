using Microsoft.JSInterop;

using Moq;

using System.Text.Json;

using VideoForensics.Client.WebApp.Services;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Client.Hosting;
using VideoForensics.Client.Ui.Shared.Services;

using Xunit;

namespace VideoForensics.Client.WebApp.Tests
{
    public class LocalMediaContentUrlProviderTests
    {
        private PairedSessionState CreateSessionState(Mock<IJSRuntime>? jsRuntime = null)
        {
            jsRuntime ??= new Mock<IJSRuntime>();
            return new PairedSessionState(jsRuntime.Object);
        }

        private Mock<IJSRuntime> SetupJSRuntimeWithSession(Guid? operatorId = null, string? sessionToken = null)
        {
            var jsRuntime = new Mock<IJSRuntime>();

            if (sessionToken != null)
            {
                var storedSession = new
                {
                    SessionToken = sessionToken,
                    OperatorId = operatorId ?? Guid.NewGuid(),
                    Role = "Admin",
                    MustChangePassword = false
                };

                string json = JsonSerializer.Serialize(storedSession);
                _ = jsRuntime
                    .Setup(j => j.InvokeAsync<string?>("vfWebAuthn.loadSession", It.IsAny<object?[]?>()))
                    .ReturnsAsync(json);
            }
            else
            {
                _ = jsRuntime
                    .Setup(j => j.InvokeAsync<string?>("vfWebAuthn.loadSession", It.IsAny<object?[]?>()))
                    .ReturnsAsync((string?)null);
            }

            return jsRuntime;
        }

        [Fact]
        public async Task GetContentUrlsAsync_NoSession_ReturnsEmpty()
        {
            Mock<IJSRuntime> jsRuntime = SetupJSRuntimeWithSession(null, null);
            PairedSessionState sessionState = CreateSessionState(jsRuntime);

            var sessionTokenService = new Mock<ISessionTokenService>();
            var ticketService = new Mock<IMediaAccessTicketService>();
            var logger = new Mock<Microsoft.Extensions.Logging.ILogger<LocalMediaContentUrlProvider>>();

            var provider = new LocalMediaContentUrlProvider(sessionState, sessionTokenService.Object, ticketService.Object, logger.Object);

            Guid[] mediaItemIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
            IReadOnlyDictionary<Guid, string> result = await provider.GetContentUrlsAsync(mediaItemIds, CancellationToken.None);

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetContentUrlsAsync_InvalidToken_ReturnsEmpty()
        {
            var operatorId = Guid.NewGuid();
            Mock<IJSRuntime> jsRuntime = SetupJSRuntimeWithSession(operatorId, "invalid_token");
            PairedSessionState sessionState = CreateSessionState(jsRuntime);

            var sessionTokenService = new Mock<ISessionTokenService>();
            _ = sessionTokenService.Setup(s => s.Validate("invalid_token")).Returns((SessionPrincipal?)null);

            var ticketService = new Mock<IMediaAccessTicketService>();
            var logger = new Mock<Microsoft.Extensions.Logging.ILogger<LocalMediaContentUrlProvider>>();

            var provider = new LocalMediaContentUrlProvider(sessionState, sessionTokenService.Object, ticketService.Object, logger.Object);

            Guid[] mediaItemIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
            IReadOnlyDictionary<Guid, string> result = await provider.GetContentUrlsAsync(mediaItemIds, CancellationToken.None);

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetContentUrlsAsync_ValidToken_IssuesTicketsUsingTokenOperatorId()
        {
            var operatorId = Guid.NewGuid();
            string sessionToken = "valid_session_token";
            Mock<IJSRuntime> jsRuntime = SetupJSRuntimeWithSession(operatorId, sessionToken);
            PairedSessionState sessionState = CreateSessionState(jsRuntime);

            var principal = new SessionPrincipal(
                operatorId,
                null,
                CredentialKind.Password,
                OperatorRole.Admin,
                Guid.NewGuid(),
                DateTime.UtcNow,
                DateTime.UtcNow.AddHours(1));

            var sessionTokenService = new Mock<ISessionTokenService>();
            _ = sessionTokenService.Setup(s => s.Validate(sessionToken)).Returns(principal);

            var mediaId1 = Guid.NewGuid();
            var mediaId2 = Guid.NewGuid();

            var ticket1 = new MediaAccessTicket("token1", DateTime.UtcNow.AddMinutes(10));
            var ticket2 = new MediaAccessTicket("token2", DateTime.UtcNow.AddMinutes(10));

            var ticketService = new Mock<IMediaAccessTicketService>();
            _ = ticketService.Setup(t => t.Issue(mediaId1, operatorId)).Returns(ticket1);
            _ = ticketService.Setup(t => t.Issue(mediaId2, operatorId)).Returns(ticket2);

            var logger = new Mock<Microsoft.Extensions.Logging.ILogger<LocalMediaContentUrlProvider>>();

            var provider = new LocalMediaContentUrlProvider(sessionState, sessionTokenService.Object, ticketService.Object, logger.Object);

            Guid[] mediaItemIds = new[] { mediaId1, mediaId2 };
            IReadOnlyDictionary<Guid, string> result = await provider.GetContentUrlsAsync(mediaItemIds, CancellationToken.None);

            Assert.NotEmpty(result);
            Assert.Equal(2, result.Count);
            Assert.True(result.ContainsKey(mediaId1));
            Assert.True(result.ContainsKey(mediaId2));
            Assert.StartsWith($"/api/v1/media/{mediaId1}/content?ticket=", result[mediaId1]);
            Assert.StartsWith($"/api/v1/media/{mediaId2}/content?ticket=", result[mediaId2]);

            // Verify the operator ID used was from the session principal, not localStorage
            ticketService.Verify(t => t.Issue(mediaId1, operatorId), Times.Once);
            ticketService.Verify(t => t.Issue(mediaId2, operatorId), Times.Once);
        }

        [Fact]
        public async Task GetContentUrlsAsync_ValidToken_DedupplicatesIds()
        {
            var operatorId = Guid.NewGuid();
            string sessionToken = "valid_session_token";
            Mock<IJSRuntime> jsRuntime = SetupJSRuntimeWithSession(operatorId, sessionToken);
            PairedSessionState sessionState = CreateSessionState(jsRuntime);

            var principal = new SessionPrincipal(
                operatorId,
                null,
                CredentialKind.Password,
                OperatorRole.Admin,
                Guid.NewGuid(),
                DateTime.UtcNow,
                DateTime.UtcNow.AddHours(1));

            var sessionTokenService = new Mock<ISessionTokenService>();
            _ = sessionTokenService.Setup(s => s.Validate(sessionToken)).Returns(principal);

            var mediaId = Guid.NewGuid();
            var ticket = new MediaAccessTicket("token1", DateTime.UtcNow.AddMinutes(10));

            var ticketService = new Mock<IMediaAccessTicketService>();
            _ = ticketService.Setup(t => t.Issue(mediaId, operatorId)).Returns(ticket);

            var logger = new Mock<Microsoft.Extensions.Logging.ILogger<LocalMediaContentUrlProvider>>();

            var provider = new LocalMediaContentUrlProvider(sessionState, sessionTokenService.Object, ticketService.Object, logger.Object);

            // Pass same ID multiple times
            Guid[] mediaItemIds = new[] { mediaId, mediaId, mediaId };
            IReadOnlyDictionary<Guid, string> result = await provider.GetContentUrlsAsync(mediaItemIds, CancellationToken.None);

            Assert.NotEmpty(result);
            _ = Assert.Single(result);
            ticketService.Verify(t => t.Issue(mediaId, operatorId), Times.Once);
        }

        [Fact]
        public async Task GetContentUrlsAsync_HonorsCancellationToken()
        {
            var operatorId = Guid.NewGuid();
            string sessionToken = "valid_session_token";
            Mock<IJSRuntime> jsRuntime = SetupJSRuntimeWithSession(operatorId, sessionToken);
            PairedSessionState sessionState = CreateSessionState(jsRuntime);

            var principal = new SessionPrincipal(
                operatorId,
                null,
                CredentialKind.Password,
                OperatorRole.Admin,
                Guid.NewGuid(),
                DateTime.UtcNow,
                DateTime.UtcNow.AddHours(1));

            var sessionTokenService = new Mock<ISessionTokenService>();
            _ = sessionTokenService.Setup(s => s.Validate(sessionToken)).Returns(principal);

            var ticketService = new Mock<IMediaAccessTicketService>();
            var logger = new Mock<Microsoft.Extensions.Logging.ILogger<LocalMediaContentUrlProvider>>();

            var provider = new LocalMediaContentUrlProvider(sessionState, sessionTokenService.Object, ticketService.Object, logger.Object);

            var cts = new CancellationTokenSource();
            cts.Cancel();

            Guid[] mediaItemIds = new[] { Guid.NewGuid() };

            // Should throw OperationCanceledException
            _ = await Assert.ThrowsAsync<OperationCanceledException>(
                async () => await provider.GetContentUrlsAsync(mediaItemIds, cts.Token));
        }

        [Fact]
        public async Task GetContentUrlsAsync_EmptyList_ReturnsEmpty()
        {
            var operatorId = Guid.NewGuid();
            string sessionToken = "valid_session_token";
            Mock<IJSRuntime> jsRuntime = SetupJSRuntimeWithSession(operatorId, sessionToken);
            PairedSessionState sessionState = CreateSessionState(jsRuntime);

            var principal = new SessionPrincipal(
                operatorId,
                null,
                CredentialKind.Password,
                OperatorRole.Admin,
                Guid.NewGuid(),
                DateTime.UtcNow,
                DateTime.UtcNow.AddHours(1));

            var sessionTokenService = new Mock<ISessionTokenService>();
            _ = sessionTokenService.Setup(s => s.Validate(sessionToken)).Returns(principal);

            var ticketService = new Mock<IMediaAccessTicketService>();
            var logger = new Mock<Microsoft.Extensions.Logging.ILogger<LocalMediaContentUrlProvider>>();

            var provider = new LocalMediaContentUrlProvider(sessionState, sessionTokenService.Object, ticketService.Object, logger.Object);

            IReadOnlyDictionary<Guid, string> result = await provider.GetContentUrlsAsync(Array.Empty<Guid>(), CancellationToken.None);

            Assert.Empty(result);
        }
    }
}
