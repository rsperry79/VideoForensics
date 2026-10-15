using System.Text.Json;

using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

using Moq;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Hosting;
using VideoForensics.Ui.Shared.Services;
using VideoForensics.WebApp.Services;

using Xunit;

namespace VideoForensics.WebApp.Tests
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
                jsRuntime
                    .Setup(j => j.InvokeAsync<string?>("vfWebAuthn.loadSession", It.IsAny<object?[]?>()))
                    .ReturnsAsync(json);
            }
            else
            {
                jsRuntime
                    .Setup(j => j.InvokeAsync<string?>("vfWebAuthn.loadSession", It.IsAny<object?[]?>()))
                    .ReturnsAsync((string?)null);
            }

            return jsRuntime;
        }

        [Fact]
        public async Task GetContentUrlsAsync_NoSession_ReturnsEmpty()
        {
            var jsRuntime = SetupJSRuntimeWithSession(null, null);
            var sessionState = CreateSessionState(jsRuntime);

            var sessionTokenService = new Mock<ISessionTokenService>();
            var ticketService = new Mock<IMediaAccessTicketService>();
            var logger = new Mock<Microsoft.Extensions.Logging.ILogger<LocalMediaContentUrlProvider>>();

            var provider = new LocalMediaContentUrlProvider(sessionState, sessionTokenService.Object, ticketService.Object, logger.Object);

            var mediaItemIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
            var result = await provider.GetContentUrlsAsync(mediaItemIds, CancellationToken.None);

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetContentUrlsAsync_InvalidToken_ReturnsEmpty()
        {
            var operatorId = Guid.NewGuid();
            var jsRuntime = SetupJSRuntimeWithSession(operatorId, "invalid_token");
            var sessionState = CreateSessionState(jsRuntime);

            var sessionTokenService = new Mock<ISessionTokenService>();
            sessionTokenService.Setup(s => s.Validate("invalid_token")).Returns((SessionPrincipal?)null);

            var ticketService = new Mock<IMediaAccessTicketService>();
            var logger = new Mock<Microsoft.Extensions.Logging.ILogger<LocalMediaContentUrlProvider>>();

            var provider = new LocalMediaContentUrlProvider(sessionState, sessionTokenService.Object, ticketService.Object, logger.Object);

            var mediaItemIds = new[] { Guid.NewGuid(), Guid.NewGuid() };
            var result = await provider.GetContentUrlsAsync(mediaItemIds, CancellationToken.None);

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetContentUrlsAsync_ValidToken_IssuesTicketsUsingTokenOperatorId()
        {
            var operatorId = Guid.NewGuid();
            var sessionToken = "valid_session_token";
            var jsRuntime = SetupJSRuntimeWithSession(operatorId, sessionToken);
            var sessionState = CreateSessionState(jsRuntime);

            var principal = new SessionPrincipal(
                operatorId,
                null,
                CredentialKind.Password,
                OperatorRole.Admin,
                Guid.NewGuid(),
                DateTime.UtcNow,
                DateTime.UtcNow.AddHours(1));

            var sessionTokenService = new Mock<ISessionTokenService>();
            sessionTokenService.Setup(s => s.Validate(sessionToken)).Returns(principal);

            var mediaId1 = Guid.NewGuid();
            var mediaId2 = Guid.NewGuid();

            var ticket1 = new MediaAccessTicket("token1", DateTime.UtcNow.AddMinutes(10));
            var ticket2 = new MediaAccessTicket("token2", DateTime.UtcNow.AddMinutes(10));

            var ticketService = new Mock<IMediaAccessTicketService>();
            ticketService.Setup(t => t.Issue(mediaId1, operatorId)).Returns(ticket1);
            ticketService.Setup(t => t.Issue(mediaId2, operatorId)).Returns(ticket2);

            var logger = new Mock<Microsoft.Extensions.Logging.ILogger<LocalMediaContentUrlProvider>>();

            var provider = new LocalMediaContentUrlProvider(sessionState, sessionTokenService.Object, ticketService.Object, logger.Object);

            var mediaItemIds = new[] { mediaId1, mediaId2 };
            var result = await provider.GetContentUrlsAsync(mediaItemIds, CancellationToken.None);

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
            var sessionToken = "valid_session_token";
            var jsRuntime = SetupJSRuntimeWithSession(operatorId, sessionToken);
            var sessionState = CreateSessionState(jsRuntime);

            var principal = new SessionPrincipal(
                operatorId,
                null,
                CredentialKind.Password,
                OperatorRole.Admin,
                Guid.NewGuid(),
                DateTime.UtcNow,
                DateTime.UtcNow.AddHours(1));

            var sessionTokenService = new Mock<ISessionTokenService>();
            sessionTokenService.Setup(s => s.Validate(sessionToken)).Returns(principal);

            var mediaId = Guid.NewGuid();
            var ticket = new MediaAccessTicket("token1", DateTime.UtcNow.AddMinutes(10));

            var ticketService = new Mock<IMediaAccessTicketService>();
            ticketService.Setup(t => t.Issue(mediaId, operatorId)).Returns(ticket);

            var logger = new Mock<Microsoft.Extensions.Logging.ILogger<LocalMediaContentUrlProvider>>();

            var provider = new LocalMediaContentUrlProvider(sessionState, sessionTokenService.Object, ticketService.Object, logger.Object);

            // Pass same ID multiple times
            var mediaItemIds = new[] { mediaId, mediaId, mediaId };
            var result = await provider.GetContentUrlsAsync(mediaItemIds, CancellationToken.None);

            Assert.NotEmpty(result);
            Assert.Single(result);
            ticketService.Verify(t => t.Issue(mediaId, operatorId), Times.Once);
        }

        [Fact]
        public async Task GetContentUrlsAsync_HonorsCancellationToken()
        {
            var operatorId = Guid.NewGuid();
            var sessionToken = "valid_session_token";
            var jsRuntime = SetupJSRuntimeWithSession(operatorId, sessionToken);
            var sessionState = CreateSessionState(jsRuntime);

            var principal = new SessionPrincipal(
                operatorId,
                null,
                CredentialKind.Password,
                OperatorRole.Admin,
                Guid.NewGuid(),
                DateTime.UtcNow,
                DateTime.UtcNow.AddHours(1));

            var sessionTokenService = new Mock<ISessionTokenService>();
            sessionTokenService.Setup(s => s.Validate(sessionToken)).Returns(principal);

            var ticketService = new Mock<IMediaAccessTicketService>();
            var logger = new Mock<Microsoft.Extensions.Logging.ILogger<LocalMediaContentUrlProvider>>();

            var provider = new LocalMediaContentUrlProvider(sessionState, sessionTokenService.Object, ticketService.Object, logger.Object);

            var cts = new CancellationTokenSource();
            cts.Cancel();

            var mediaItemIds = new[] { Guid.NewGuid() };

            // Should throw OperationCanceledException
            await Assert.ThrowsAsync<OperationCanceledException>(
                async () => await provider.GetContentUrlsAsync(mediaItemIds, cts.Token));
        }

        [Fact]
        public async Task GetContentUrlsAsync_EmptyList_ReturnsEmpty()
        {
            var operatorId = Guid.NewGuid();
            var sessionToken = "valid_session_token";
            var jsRuntime = SetupJSRuntimeWithSession(operatorId, sessionToken);
            var sessionState = CreateSessionState(jsRuntime);

            var principal = new SessionPrincipal(
                operatorId,
                null,
                CredentialKind.Password,
                OperatorRole.Admin,
                Guid.NewGuid(),
                DateTime.UtcNow,
                DateTime.UtcNow.AddHours(1));

            var sessionTokenService = new Mock<ISessionTokenService>();
            sessionTokenService.Setup(s => s.Validate(sessionToken)).Returns(principal);

            var ticketService = new Mock<IMediaAccessTicketService>();
            var logger = new Mock<Microsoft.Extensions.Logging.ILogger<LocalMediaContentUrlProvider>>();

            var provider = new LocalMediaContentUrlProvider(sessionState, sessionTokenService.Object, ticketService.Object, logger.Object);

            var result = await provider.GetContentUrlsAsync(Array.Empty<Guid>(), CancellationToken.None);

            Assert.Empty(result);
        }
    }
}
