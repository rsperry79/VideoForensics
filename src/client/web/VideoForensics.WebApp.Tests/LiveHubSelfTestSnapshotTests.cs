using System.Security.Claims;

using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using VideoForensics.Api.Contracts;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Providers.Common.Contracts;
using VideoForensics.WebApp.Auth;
using VideoForensics.WebApp.Hubs;

using Xunit;

namespace VideoForensics.WebApp.Tests
{
    public class LiveHubSelfTestSnapshotTests
    {
        private const string SelfTestStatusMethod = "SelfTestStatus";

        private static (LiveHub Hub, Mock<ISingleClientProxy> Caller, Mock<IGroupManager> Groups, Mock<IRingSelfTestService> SelfTest) BuildHub(
            string? roleClaim,
            CancellationToken connectionAborted = default)
        {
            var download = new Mock<IVideoDownloadService>();
            download.Setup(s => s.GetProgress()).Returns(new DownloadStatus(false, 0, 0, 0L, string.Empty, 0, 0, 0L, 0, 0.0));
            download.Setup(s => s.GetCurrentDevice()).Returns((0, 0, string.Empty));
            download.Setup(s => s.DrainActivityLog()).Returns(new List<string>());

            var selfTest = new Mock<IRingSelfTestService>();
            selfTest.Setup(s => s.GetStatusAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SelfTestStatusDto(SelfTestRunStatus.Completed, null, null, null));

            IServiceProvider services = new ServiceCollection()
                .AddSingleton(download.Object)
                .AddSingleton(selfTest.Object)
                .BuildServiceProvider();
            IServiceScopeFactory scopeFactory = services.GetRequiredService<IServiceScopeFactory>();

            var caller = new Mock<ISingleClientProxy>();
            var clients = new Mock<IHubCallerClients>();
            clients.Setup(c => c.Caller).Returns(caller.Object);

            var identity = roleClaim is null
                ? new ClaimsIdentity()
                : new ClaimsIdentity(new[] { new Claim(VideoForensicsClaimTypes.Role, roleClaim) });
            var context = new Mock<HubCallerContext>();
            context.Setup(c => c.ConnectionId).Returns("conn-1");
            context.Setup(c => c.User).Returns(new ClaimsPrincipal(identity));
            context.Setup(c => c.ConnectionAborted).Returns(connectionAborted);

            var groups = new Mock<IGroupManager>();
            var hub = new LiveHub(new Mock<ILiveConnectionTracker>().Object, scopeFactory, NullLogger<LiveHub>.Instance)
            {
                Clients = clients.Object,
                Groups = groups.Object,
                Context = context.Object,
            };

            return (hub, caller, groups, selfTest);
        }

        [Fact]
        public async Task LiveHub_OnConnectedAdmin_SendsOneSelfTestStatusSnapshotToCaller()
        {
            (LiveHub hub, Mock<ISingleClientProxy> caller, _, _) = BuildHub("Admin");

            await hub.OnConnectedAsync();

            caller.Verify(
                p => p.SendCoreAsync(
                    SelfTestStatusMethod,
                    It.Is<object?[]>(a => a.Length == 1 && a[0] is SelfTestStatusDto),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task LiveHub_OnConnectedAdmin_AddsCallerToAdminsGroup()
        {
            (LiveHub hub, _, Mock<IGroupManager> groups, _) = BuildHub("SuperAdmin");

            await hub.OnConnectedAsync();

            groups.Verify(g => g.AddToGroupAsync("conn-1", "admins", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task LiveHub_SelfTestSnapshotThrows_StillCompletesConnectAndSendsNoSelfTestStatus()
        {
            (LiveHub hub, Mock<ISingleClientProxy> caller, _, Mock<IRingSelfTestService> selfTest) = BuildHub("Admin");
            selfTest.Setup(s => s.GetStatusAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("self-test status unavailable"));

            await hub.OnConnectedAsync();

            caller.Verify(
                p => p.SendCoreAsync(SelfTestStatusMethod, It.IsAny<object?[]>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task LiveHub_SelfTestSnapshotThrowsWhileConnectionAborting_RethrowsOriginalException()
        {
            using var aborting = new CancellationTokenSource();
            aborting.Cancel();
            (LiveHub hub, _, _, Mock<IRingSelfTestService> selfTest) = BuildHub("Admin", aborting.Token);
            selfTest.Setup(s => s.GetStatusAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("self-test status unavailable"));

            await Assert.ThrowsAsync<InvalidOperationException>(() => hub.OnConnectedAsync());
        }

        [Fact]
        public async Task LiveHub_OnConnectedReadOnly_DoesNotSendSelfTestStatus()
        {
            (LiveHub hub, Mock<ISingleClientProxy> caller, _, Mock<IRingSelfTestService> selfTest) = BuildHub("ReadOnly");

            await hub.OnConnectedAsync();

            caller.Verify(
                p => p.SendCoreAsync(SelfTestStatusMethod, It.IsAny<object?[]>(), It.IsAny<CancellationToken>()),
                Times.Never);
            selfTest.Verify(s => s.GetStatusAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task LiveHub_OnConnectedNoRoleClaim_DoesNotSendSelfTestStatus()
        {
            (LiveHub hub, Mock<ISingleClientProxy> caller, _, _) = BuildHub(null);

            await hub.OnConnectedAsync();

            caller.Verify(
                p => p.SendCoreAsync(SelfTestStatusMethod, It.IsAny<object?[]>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }
    }
}
