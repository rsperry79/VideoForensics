using System.Security.Claims;

using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.WebApp.Hubs;

using Xunit;

namespace VideoForensics.WebApp.Tests
{
    public class LiveHubLiveViewSubscriptionTests
    {
        private const string ConnectionId = "conn-live-view";

        private static (LiveHub Hub, Mock<IGroupManager> Groups, Mock<ILiveViewSessionRepository> Sessions) BuildHub()
        {
            var sessions = new Mock<ILiveViewSessionRepository>();

            IServiceProvider services = new ServiceCollection()
                .AddSingleton(sessions.Object)
                .BuildServiceProvider();
            IServiceScopeFactory scopeFactory = services.GetRequiredService<IServiceScopeFactory>();

            var context = new Mock<HubCallerContext>();
            context.Setup(c => c.ConnectionId).Returns(ConnectionId);
            context.Setup(c => c.User).Returns(new ClaimsPrincipal(new ClaimsIdentity()));

            var groups = new Mock<IGroupManager>();

            var hub = new LiveHub(new Mock<ILiveConnectionTracker>().Object, scopeFactory, NullLogger<LiveHub>.Instance)
            {
                Clients = new Mock<IHubCallerClients>().Object,
                Groups = groups.Object,
                Context = context.Object,
            };

            return (hub, groups, sessions);
        }

        [Fact]
        public async Task SubscribeLiveView_UnknownSession_ThrowsHubException()
        {
            (LiveHub hub, Mock<IGroupManager> groups, Mock<ILiveViewSessionRepository> sessions) = BuildHub();
            Guid sessionId = Guid.NewGuid();
            sessions.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>())).ReturnsAsync((LiveViewSession?)null);

            await Assert.ThrowsAsync<HubException>(() => hub.SubscribeLiveView(sessionId));

            groups.Verify(g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task SubscribeLiveView_ValidSession_AddsConnectionToSessionGroup()
        {
            (LiveHub hub, Mock<IGroupManager> groups, Mock<ILiveViewSessionRepository> sessions) = BuildHub();
            Guid sessionId = Guid.NewGuid();
            sessions.Setup(r => r.GetByIdAsync(sessionId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new LiveViewSession { Id = sessionId });

            await hub.SubscribeLiveView(sessionId);

            groups.Verify(
                g => g.AddToGroupAsync(ConnectionId, LiveHubMethods.LiveViewGroup(sessionId), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task UnsubscribeLiveView_ValidSession_RemovesConnectionFromGroup()
        {
            (LiveHub hub, Mock<IGroupManager> groups, _) = BuildHub();
            Guid sessionId = Guid.NewGuid();

            await hub.UnsubscribeLiveView(sessionId);

            groups.Verify(
                g => g.RemoveFromGroupAsync(ConnectionId, LiveHubMethods.LiveViewGroup(sessionId), It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }
}
