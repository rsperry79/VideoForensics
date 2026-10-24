using CloudflaredKit;
using CloudflaredKit.Models;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using VideoForensics.Client.Hosting;

namespace VideoForensics.Client.Hosting.Tests
{
    /// <summary>A tunnel start that fails is logged and shown as Failed with the reason; a start that works is logged as running.</summary>
    public class CloudflaredTunnelLoggingTests
    {
        private const string ValidToken = "test-permanent-tunnel-token";

        private static int CountLogs(Mock<ILogger<CloudflaredTunnelService>> logger, LogLevel level)
        {
            return logger.Invocations.Count(i => i.Method.Name == nameof(ILogger.Log) && (LogLevel)i.Arguments[0] == level);
        }

        [Fact]
        public async Task StartPermanentTunnelAsync_CloudflaredFails_LogsErrorAndReportsFailedWithReason()
        {
            var cloudflared = new Mock<ICloudflaredService>();
            _ = cloudflared.Setup(c => c.StartAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("cloudflared process exited during startup"));
            var logger = new Mock<ILogger<CloudflaredTunnelService>>();
            var service = new CloudflaredTunnelService(cloudflared.Object, new Mock<IOptionsMonitorCache<CloudflaredOptions>>().Object, logger.Object);

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.StartPermanentTunnelAsync(ValidToken, CancellationToken.None));

            TunnelState state = service.GetState();
            Assert.Equal(TunnelStatus.Failed, state.Status);
            Assert.Contains("Could not start the Cloudflare tunnel", state.ErrorMessage);
            Assert.Contains("cloudflared process exited during startup", state.ErrorMessage);
            Assert.Equal(1, CountLogs(logger, LogLevel.Error));
        }

        [Fact]
        public async Task StartPermanentTunnelAsync_Succeeds_LogsInformationAndReportsRunning()
        {
            var cloudflared = new Mock<ICloudflaredService>();
            _ = cloudflared.Setup(c => c.StartAsync(It.IsAny<CancellationToken>())).Returns(Task.FromResult<TunnelInfo>(null!));
            var logger = new Mock<ILogger<CloudflaredTunnelService>>();
            var service = new CloudflaredTunnelService(cloudflared.Object, new Mock<IOptionsMonitorCache<CloudflaredOptions>>().Object, logger.Object);

            await service.StartPermanentTunnelAsync(ValidToken, CancellationToken.None);

            Assert.Equal(TunnelStatus.Running, service.GetState().Status);
            Assert.Equal(0, CountLogs(logger, LogLevel.Error));
            Assert.Equal(1, CountLogs(logger, LogLevel.Information));
        }
    }
}
