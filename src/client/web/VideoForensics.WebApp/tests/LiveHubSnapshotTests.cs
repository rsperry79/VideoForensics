using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using System.Security.Claims;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.WebApp.Hubs;
using VideoForensics.Core.Api.Contracts;

using Xunit;

namespace VideoForensics.Client.WebApp.Tests
{
    public class LiveHubSnapshotTests
    {
        private const string DownloadProgressMethod = "DownloadProgress";

        private static (LiveHub Hub, Mock<ISingleClientProxy> Caller) BuildHub()
        {
            IServiceProvider services = new ServiceCollection().BuildServiceProvider();
            IServiceScopeFactory scopeFactory = services.GetRequiredService<IServiceScopeFactory>();

            var caller = new Mock<ISingleClientProxy>();
            var clients = new Mock<IHubCallerClients>();
            _ = clients.Setup(c => c.Caller).Returns(caller.Object);

            var context = new Mock<HubCallerContext>();
            _ = context.Setup(c => c.ConnectionId).Returns("conn-1");
            _ = context.Setup(c => c.User).Returns(new ClaimsPrincipal(new ClaimsIdentity()));

            var hub = new LiveHub(new Mock<ILiveConnectionTracker>().Object, scopeFactory, NullLogger<LiveHub>.Instance)
            {
                Clients = clients.Object,
                Groups = new Mock<IGroupManager>().Object,
                Context = context.Object,
            };

            return (hub, caller);
        }

        [Fact]
        public async Task LiveHub_OnConnected_SendsOneDownloadProgressSnapshotToCaller()
        {
            (LiveHub hub, Mock<ISingleClientProxy> caller) = BuildHub();

            await hub.OnConnectedAsync();

            caller.Verify(
                p => p.SendCoreAsync(
                    DownloadProgressMethod,
                    It.Is<object?[]>(a => a.Length == 1 && a[0] is DownloadProgressDto),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task LiveHub_OnConnected_SnapshotIsIdleWithNoActivity()
        {
            (LiveHub hub, Mock<ISingleClientProxy> caller) = BuildHub();
            DownloadProgressDto? sent = null;
            _ = caller.Setup(p => p.SendCoreAsync(DownloadProgressMethod, It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
                .Callback<string, object?[], CancellationToken>((_, args, _) => sent = args[0] as DownloadProgressDto)
                .Returns(Task.CompletedTask);

            await hub.OnConnectedAsync();

            Assert.NotNull(sent);
            Assert.Empty(sent!.Activity);
            Assert.False(sent.Progress.IsDownloading);
            Assert.Equal(0, sent.CurrentDeviceIndex);
            Assert.Equal(0, sent.CurrentDeviceTotal);
        }

        [Fact]
        public async Task LiveHub_OnConnected_SendFails_StillCompletesConnect()
        {
            (LiveHub hub, Mock<ISingleClientProxy> caller) = BuildHub();
            _ = caller.Setup(p => p.SendCoreAsync(DownloadProgressMethod, It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("send failed"));

            await hub.OnConnectedAsync();

            caller.Verify(
                p => p.SendCoreAsync(DownloadProgressMethod, It.IsAny<object?[]>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }
}
