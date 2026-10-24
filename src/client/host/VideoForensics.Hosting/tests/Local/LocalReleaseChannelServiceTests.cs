using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Hosting.Local;

namespace VideoForensics.Client.Hosting.Tests.Local
{
    public class LocalReleaseChannelServiceTests
    {
        [Fact]
        public async Task LocalReleaseChannelService_Get_ReturnsConfiguredChannel()
        {
            var config = new ForensicsConfiguration { ReleaseChannel = UpdateReleaseChannel.Testing };
            var configService = new Mock<IForensicsConfigurationService>();
            var service = new LocalReleaseChannelService(config, configService.Object);

            UpdateReleaseChannel result = await service.GetReleaseChannelAsync(CancellationToken.None);

            Assert.Equal(UpdateReleaseChannel.Testing, result);
            configService.Verify(s => s.SaveConfigurationAsync(It.IsAny<IForensicsConfiguration>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task LocalReleaseChannelService_Set_UpdatesConfigSavesOnceAndReturnsChannel()
        {
            var config = new ForensicsConfiguration { ReleaseChannel = UpdateReleaseChannel.Stable };
            var configService = new Mock<IForensicsConfigurationService>();
            using var cts = new CancellationTokenSource();
            var service = new LocalReleaseChannelService(config, configService.Object);

            UpdateReleaseChannel result = await service.SetReleaseChannelAsync(UpdateReleaseChannel.Testing, cts.Token);

            Assert.Equal(UpdateReleaseChannel.Testing, result);
            Assert.Equal(UpdateReleaseChannel.Testing, config.ReleaseChannel);
            configService.Verify(s => s.SaveConfigurationAsync(config, cts.Token), Times.Once);
        }
    }
}
