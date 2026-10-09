using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using VideoForensics.Api.Contracts;
using VideoForensics.Hosting;
using VideoForensics.WebApp.Hubs;

using Xunit;

namespace VideoForensics.WebApp.Tests
{
    public class SelfTestStatusBroadcastServiceTests
    {
        private const string SelfTestStatusMethod = "SelfTestStatus";

        // The service ticks every 1000 ms, so the first tick fires about 1 s after start. The waits
        // below allow for that plus scheduling jitter; they are upper bounds, not fixed sleeps.
        private static readonly TimeSpan MaxWait = TimeSpan.FromSeconds(5);

        private static (SelfTestStatusBroadcastService Service, Mock<IClientProxy> AdminsGroup, Mock<IRingSelfTestService> SelfTest) Build()
        {
            var selfTest = new Mock<IRingSelfTestService>();
            var adminsGroup = new Mock<IClientProxy>();
            var clients = new Mock<IHubClients>();
            clients.Setup(c => c.Group("admins")).Returns(adminsGroup.Object);
            var hubContext = new Mock<IHubContext<LiveHub>>();
            hubContext.Setup(h => h.Clients).Returns(clients.Object);

            IServiceProvider services = new ServiceCollection()
                .AddSingleton(selfTest.Object)
                .BuildServiceProvider();

            var service = new SelfTestStatusBroadcastService(
                services.GetRequiredService<IServiceScopeFactory>(),
                hubContext.Object,
                new SelfTestStatusChangeDetector(),
                NullLogger<SelfTestStatusBroadcastService>.Instance);

            return (service, adminsGroup, selfTest);
        }

        private static bool HasStatus(object?[] args, SelfTestRunStatus status)
        {
            return args.Length == 1 && args[0] is SelfTestStatusDto dto && dto.Status == status;
        }

        private static async Task WaitUntilAsync(Func<bool> condition)
        {
            DateTime deadline = DateTime.UtcNow + MaxWait;
            while (!condition() && DateTime.UtcNow < deadline)
            {
                await Task.Delay(50);
            }
        }

        [Fact]
        public async Task SelfTestStatusBroadcastService_UnchangedStatus_SendsOnlyOnce()
        {
            (SelfTestStatusBroadcastService service, Mock<IClientProxy> adminsGroup, Mock<IRingSelfTestService> selfTest) = Build();
            selfTest.Setup(s => s.GetStatusAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SelfTestStatusDto(SelfTestRunStatus.Idle));
            using var cts = new CancellationTokenSource();

            await service.StartAsync(cts.Token);
            await WaitUntilAsync(() => adminsGroup.Invocations.Count >= 1);
            await Task.Delay(1500); // at least one more tick with an identical status
            await service.StopAsync(CancellationToken.None);

            adminsGroup.Verify(
                p => p.SendCoreAsync(SelfTestStatusMethod, It.Is<object?[]>(a => a.Length == 1 && a[0] is SelfTestStatusDto), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task SelfTestStatusBroadcastService_StatusChanges_SendsNewStatus()
        {
            (SelfTestStatusBroadcastService service, Mock<IClientProxy> adminsGroup, Mock<IRingSelfTestService> selfTest) = Build();
            SelfTestStatusDto current = new(SelfTestRunStatus.Idle);
            selfTest.Setup(s => s.GetStatusAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => current);
            using var cts = new CancellationTokenSource();

            await service.StartAsync(cts.Token);
            await WaitUntilAsync(() => adminsGroup.Invocations.Count >= 1);
            current = new SelfTestStatusDto(SelfTestRunStatus.Running, DateTime.UtcNow);
            await WaitUntilAsync(() => adminsGroup.Invocations.Count >= 2);
            await service.StopAsync(CancellationToken.None);

            adminsGroup.Verify(
                p => p.SendCoreAsync(SelfTestStatusMethod, It.Is<object?[]>(a => HasStatus(a, SelfTestRunStatus.Running)), It.IsAny<CancellationToken>()),
                Times.Once);
        }
    }
}
