using System.Security.Claims;

using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;

using Moq;

using VideoForensics.Api.Contracts;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Providers.Common.Contracts;
using VideoForensics.WebApp.Hubs;

using Xunit;

namespace VideoForensics.WebApp.Tests
{
    public class LiveHubSnapshotTests
    {
        private const string DownloadProgressMethod = "DownloadProgress";

        private static (LiveHub Hub, Mock<ISingleClientProxy> Caller, Mock<IVideoDownloadService> Download) BuildHub()
        {
            var download = new Mock<IVideoDownloadService>();
            download.Setup(s => s.GetProgress()).Returns(new DownloadStatus(true, 2, 5, 300L, "b.mp4", 4, 9, 900L, 1, 2.0));
            download.Setup(s => s.GetCurrentDevice()).Returns((2, 5, "Front door"));
            download.Setup(s => s.DrainActivityLog()).Returns(new List<string> { "pending line" });

            IServiceProvider services = new ServiceCollection()
                .AddSingleton(download.Object)
                .BuildServiceProvider();
            IServiceScopeFactory scopeFactory = services.GetRequiredService<IServiceScopeFactory>();

            var caller = new Mock<ISingleClientProxy>();
            var clients = new Mock<IHubCallerClients>();
            clients.Setup(c => c.Caller).Returns(caller.Object);

            var context = new Mock<HubCallerContext>();
            context.Setup(c => c.ConnectionId).Returns("conn-1");
            context.Setup(c => c.User).Returns(new ClaimsPrincipal(new ClaimsIdentity()));

            var hub = new LiveHub(new Mock<ILiveConnectionTracker>().Object, scopeFactory)
            {
                Clients = clients.Object,
                Groups = new Mock<IGroupManager>().Object,
                Context = context.Object,
            };

            return (hub, caller, download);
        }

        [Fact]
        public async Task LiveHub_OnConnected_SendsOneDownloadProgressSnapshotToCaller()
        {
            (LiveHub hub, Mock<ISingleClientProxy> caller, _) = BuildHub();

            await hub.OnConnectedAsync();

            caller.Verify(
                p => p.SendCoreAsync(
                    DownloadProgressMethod,
                    It.Is<object?[]>(a => a.Length == 1 && a[0] is DownloadProgressDto),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task LiveHub_OnConnected_SnapshotHasEmptyActivityAndDoesNotDrainLog()
        {
            (LiveHub hub, Mock<ISingleClientProxy> caller, Mock<IVideoDownloadService> download) = BuildHub();
            DownloadProgressDto? sent = null;
            caller.Setup(p => p.SendCoreAsync(DownloadProgressMethod, It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
                .Callback<string, object?[], CancellationToken>((_, args, _) => sent = args[0] as DownloadProgressDto)
                .Returns(Task.CompletedTask);

            await hub.OnConnectedAsync();

            Assert.NotNull(sent);
            Assert.Empty(sent!.Activity);
            Assert.Equal(2, sent.CurrentDeviceIndex);
            download.Verify(s => s.DrainActivityLog(), Times.Never);
        }
    }
}
