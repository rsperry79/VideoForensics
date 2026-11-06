using CloudflaredKit;
using CloudflaredKit.Models;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace VideoForensics.Client.Hosting.Tests
{
    /// <summary>Shutdown stops a running tunnel on purpose and does not report that exit as a failure.</summary>
    public class CloudflaredTunnelDisposeTests
    {
        private const string ValidToken = "test-permanent-tunnel-token";

        private static int CountLogs(Mock<ILogger<CloudflaredTunnelService>> logger, LogLevel level)
        {
            return logger.Invocations.Count(i => i.Method.Name == nameof(ILogger.Log) && Equals(i.Arguments[0], level));
        }

        private static async Task<(CloudflaredTunnelService Service, Mock<ICloudflaredService> Cloudflared, Mock<ILogger<CloudflaredTunnelService>> Logger)> RunningServiceAsync()
        {
            var cloudflared = new Mock<ICloudflaredService>();
            _ = cloudflared.Setup(c => c.StartAsync(It.IsAny<CancellationToken>())).Returns(Task.FromResult<TunnelInfo>(null!));
            var logger = new Mock<ILogger<CloudflaredTunnelService>>();
            var service = new CloudflaredTunnelService(cloudflared.Object, new Mock<IOptionsMonitorCache<CloudflaredOptions>>().Object, logger.Object);
            await service.StartPermanentTunnelAsync(ValidToken, CancellationToken.None);
            return (service, cloudflared, logger);
        }

        [Fact]
        public async Task DisposeAsync_TunnelRunning_StopsCloudflaredAndReportsStopped()
        {
            (CloudflaredTunnelService service, Mock<ICloudflaredService> cloudflared, _) = await RunningServiceAsync();

            await service.DisposeAsync();

            cloudflared.Verify(c => c.StopAsync(It.IsAny<CancellationToken>()), Times.Once);
            Assert.Equal(TunnelStatus.Stopped, service.GetState().Status);
        }

        [Fact]
        public async Task DisposeAsync_TunnelNeverStarted_DoesNotStopCloudflared()
        {
            var cloudflared = new Mock<ICloudflaredService>();
            var service = new CloudflaredTunnelService(cloudflared.Object, new Mock<IOptionsMonitorCache<CloudflaredOptions>>().Object);

            await service.DisposeAsync();

            cloudflared.Verify(c => c.StopAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task DisposeAsync_CalledTwice_StopsCloudflaredOnce()
        {
            (CloudflaredTunnelService service, Mock<ICloudflaredService> cloudflared, _) = await RunningServiceAsync();

            await service.DisposeAsync();
            await service.DisposeAsync();

            cloudflared.Verify(c => c.StopAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DisposeAsync_StopFails_LogsErrorWithoutThrowing()
        {
            (CloudflaredTunnelService service, Mock<ICloudflaredService> cloudflared, Mock<ILogger<CloudflaredTunnelService>> logger) = await RunningServiceAsync();
            _ = cloudflared.Setup(c => c.StopAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("stop failed"));

            await service.DisposeAsync();

            Assert.Equal(1, CountLogs(logger, LogLevel.Error));
        }

        [Fact]
        public async Task TunnelExitedUnexpectedly_AfterDispose_IsNotLoggedAsAnError()
        {
            (CloudflaredTunnelService service, Mock<ICloudflaredService> cloudflared, Mock<ILogger<CloudflaredTunnelService>> logger) = await RunningServiceAsync();

            await service.DisposeAsync();
            cloudflared.Raise(c => c.TunnelExitedUnexpectedly += null, 1);

            Assert.Equal(0, CountLogs(logger, LogLevel.Error));
        }
    }
}
