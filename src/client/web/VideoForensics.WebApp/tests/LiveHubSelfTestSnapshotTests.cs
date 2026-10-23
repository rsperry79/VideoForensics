using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using System.Security.Claims;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.WebApp.Auth;
using VideoForensics.Client.WebApp.Hubs;
using VideoForensics.Core.Api.Contracts;
using VideoForensics.Core.Providers.Common.Contracts;

using Xunit;

namespace VideoForensics.Client.WebApp.Tests
{
    public class LiveHubSelfTestSnapshotTests
    {
        private const string SelfTestStatusMethod = "SelfTestStatus";

        private static (LiveHub Hub, Mock<ISingleClientProxy> Caller, Mock<IGroupManager> Groups, Mock<IRingSelfTestService> SelfTest) BuildHub(
            string? roleClaim,
            CancellationToken connectionAborted = default)
        {
            var download = new Mock<IVideoDownloadService>();
            _ = download.Setup(s => s.GetProgress()).Returns(new DownloadStatus(false, 0, 0, 0L, string.Empty, 0, 0, 0L, 0, 0.0));
            _ = download.Setup(s => s.GetCurrentDevice()).Returns((0, 0, string.Empty));
            _ = download.Setup(s => s.DrainActivityLog()).Returns([]);

            var selfTest = new Mock<IRingSelfTestService>();
            _ = selfTest.Setup(s => s.GetStatusAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SelfTestStatusDto(SelfTestRunStatus.Completed, null, null, null));

            IServiceProvider services = new ServiceCollection()
                .AddSingleton(download.Object)
                .AddSingleton(selfTest.Object)
                .BuildServiceProvider();
            IServiceScopeFactory scopeFactory = services.GetRequiredService<IServiceScopeFactory>();

            var caller = new Mock<ISingleClientProxy>();
            var clients = new Mock<IHubCallerClients>();
            _ = clients.Setup(c => c.Caller).Returns(caller.Object);

            ClaimsIdentity identity = roleClaim is null
                ? new ClaimsIdentity()
                : new ClaimsIdentity(new[] { new Claim(VideoForensicsClaimTypes.Role, roleClaim) });
            var context = new Mock<HubCallerContext>();
            _ = context.Setup(c => c.ConnectionId).Returns("conn-1");
            _ = context.Setup(c => c.User).Returns(new ClaimsPrincipal(identity));
            _ = context.Setup(c => c.ConnectionAborted).Returns(connectionAborted);

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
            _ = selfTest.Setup(s => s.GetStatusAsync(It.IsAny<CancellationToken>()))
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
            _ = selfTest.Setup(s => s.GetStatusAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("self-test status unavailable"));

            _ = await Assert.ThrowsAsync<InvalidOperationException>(hub.OnConnectedAsync);
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
