using CloudflaredKit;
using CloudflaredKit.Models;

using Microsoft.Extensions.Options;

namespace VideoForensics.Client.Hosting.Tests
{
    public class CloudflaredTunnelServiceTests
    {
        private const string ValidToken = "test-permanent-tunnel-token";

        [Fact]
        public async Task StartPermanentTunnelAsync_ValidToken_StoresTokenAndReportsRunning()
        {
            var cloudflared = new Mock<ICloudflaredService>();
            _ = cloudflared.Setup(c => c.StartAsync(It.IsAny<CancellationToken>())).Returns(Task.FromResult<TunnelInfo>(null!));
            var cache = new Mock<IOptionsMonitorCache<CloudflaredOptions>>();
            var service = new CloudflaredTunnelService(cloudflared.Object, cache.Object);

            await service.StartPermanentTunnelAsync(ValidToken, CancellationToken.None);

            cache.Verify(c => c.TryAdd(Options.DefaultName, It.Is<CloudflaredOptions>(o => o.TunnelToken == ValidToken)), Times.Once);
            TunnelState state = service.GetState();
            Assert.Equal(TunnelKind.Named, state.Kind);
            Assert.Equal(TunnelStatus.Running, state.Status);
            Assert.Null(state.ErrorMessage);
        }

        [Fact]
        public async Task StartPermanentTunnelAsync_CalledWhileRunning_DoesNotStartAgain()
        {
            var cloudflared = new Mock<ICloudflaredService>();
            _ = cloudflared.Setup(c => c.StartAsync(It.IsAny<CancellationToken>())).Returns(Task.FromResult<TunnelInfo>(null!));
            var service = new CloudflaredTunnelService(cloudflared.Object, new Mock<IOptionsMonitorCache<CloudflaredOptions>>().Object);

            await service.StartPermanentTunnelAsync(ValidToken, CancellationToken.None);
            await service.StartPermanentTunnelAsync(ValidToken, CancellationToken.None);

            cloudflared.Verify(c => c.StartAsync(It.IsAny<CancellationToken>()), Times.Once);
            Assert.Equal(TunnelStatus.Running, service.GetState().Status);
        }

        [Fact]
        public async Task StopAsync_AfterStart_StopsProcessAndReportsStopped()
        {
            var cloudflared = new Mock<ICloudflaredService>();
            _ = cloudflared.Setup(c => c.StartAsync(It.IsAny<CancellationToken>())).Returns(Task.FromResult<TunnelInfo>(null!));
            _ = cloudflared.Setup(c => c.StopAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            var service = new CloudflaredTunnelService(cloudflared.Object, new Mock<IOptionsMonitorCache<CloudflaredOptions>>().Object);
            await service.StartPermanentTunnelAsync(ValidToken, CancellationToken.None);

            await service.StopAsync(CancellationToken.None);

            cloudflared.Verify(c => c.StopAsync(It.IsAny<CancellationToken>()), Times.Once);
            TunnelState state = service.GetState();
            Assert.Equal(TunnelStatus.Stopped, state.Status);
            Assert.Equal(TunnelKind.None, state.Kind);
            Assert.Null(state.PublicUrl);
        }

        [Fact]
        public async Task StartPermanentTunnelAsync_ProcessFails_ReportsFailedWithMessageAndRethrows()
        {
            var cloudflared = new Mock<ICloudflaredService>();
            _ = cloudflared.Setup(c => c.StartAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("cloudflared exited with code 1"));
            var service = new CloudflaredTunnelService(cloudflared.Object, new Mock<IOptionsMonitorCache<CloudflaredOptions>>().Object);

            _ = await Assert.ThrowsAsync<InvalidOperationException>(() => service.StartPermanentTunnelAsync(ValidToken, CancellationToken.None));

            TunnelState state = service.GetState();
            Assert.Equal(TunnelStatus.Failed, state.Status);
            Assert.Equal("cloudflared exited with code 1", state.ErrorMessage);
        }

        [Fact]
        public async Task StartPermanentTunnelAsync_Cancelled_ReturnsToStoppedNotStarting()
        {
            var cloudflared = new Mock<ICloudflaredService>();
            _ = cloudflared.Setup(c => c.StartAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new OperationCanceledException());
            var service = new CloudflaredTunnelService(cloudflared.Object, new Mock<IOptionsMonitorCache<CloudflaredOptions>>().Object);

            _ = await Assert.ThrowsAsync<OperationCanceledException>(() => service.StartPermanentTunnelAsync(ValidToken, CancellationToken.None));

            Assert.Equal(TunnelStatus.Stopped, service.GetState().Status);
        }

        [Fact]
        public async Task StartPermanentTunnelAsync_EmptyToken_ThrowsWithoutStarting()
        {
            var cloudflared = new Mock<ICloudflaredService>();
            var service = new CloudflaredTunnelService(cloudflared.Object, new Mock<IOptionsMonitorCache<CloudflaredOptions>>().Object);

            _ = await Assert.ThrowsAnyAsync<ArgumentException>(() => service.StartPermanentTunnelAsync("  ", CancellationToken.None));

            cloudflared.Verify(c => c.StartAsync(It.IsAny<CancellationToken>()), Times.Never);
            Assert.Equal(TunnelStatus.Stopped, service.GetState().Status);
        }
    }
}
