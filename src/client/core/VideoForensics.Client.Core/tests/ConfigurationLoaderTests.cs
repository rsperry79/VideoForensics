using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Client.Core.Tests
{
    public class ConfigurationLoaderTests
    {
        [Fact]
        public async Task LoadAndApplyAsync_PropagatesExportsLocation()
        {
            var loadedConfig = new ForensicsConfiguration
            {
                ExportsLocation = "exports-path",
            };

            var configService = new Mock<IForensicsConfigurationService>();
            _ = configService.Setup(s => s.LoadConfigurationAsync("", It.IsAny<CancellationToken>()))
                .ReturnsAsync(loadedConfig);

            var runtimeConfig = new ForensicsConfiguration();

            await ConfigurationLoader.LoadAndApplyAsync(configService.Object, runtimeConfig, NullLogger.Instance, CancellationToken.None);

            Assert.Equal("exports-path", runtimeConfig.ExportsLocation);
        }

        [Fact]
        public async Task LoadAndApplyAsync_PropagatesAllSixLocationProperties()
        {
            var loadedConfig = new ForensicsConfiguration
            {
                DownloadLocation = "download-path",
                QueryExportLocation = "query-export-path",
                DatabaseLocation = "database-path",
                TempDownloadLocation = "temp-download-path",
                LogsLocation = "logs-path",
                ReportsLocation = "reports-path",
            };

            var configService = new Mock<IForensicsConfigurationService>();
            _ = configService.Setup(s => s.LoadConfigurationAsync("", It.IsAny<CancellationToken>()))
                .ReturnsAsync(loadedConfig);

            var runtimeConfig = new ForensicsConfiguration();

            await ConfigurationLoader.LoadAndApplyAsync(configService.Object, runtimeConfig, NullLogger.Instance, CancellationToken.None);

            Assert.Equal("download-path", runtimeConfig.DownloadLocation);
            Assert.Equal("query-export-path", runtimeConfig.QueryExportLocation);
            Assert.Equal("database-path", runtimeConfig.DatabaseLocation);
            Assert.Equal("temp-download-path", runtimeConfig.TempDownloadLocation);
            Assert.Equal("logs-path", runtimeConfig.LogsLocation);
            Assert.Equal("reports-path", runtimeConfig.ReportsLocation);
        }

        [Fact]
        public async Task LoadAndApplyAsync_PropagatesSavedInternetAddressAndTier()
        {
            var loadedConfig = new ForensicsConfiguration
            {
                InternetServerUrl = "https://video.example.com",
                ConfiguredNetworkTier = NetworkTier.Internet,
            };

            var configService = new Mock<IForensicsConfigurationService>();
            _ = configService.Setup(s => s.LoadConfigurationAsync("", It.IsAny<CancellationToken>()))
                .ReturnsAsync(loadedConfig);

            var runtimeConfig = new ForensicsConfiguration();

            await ConfigurationLoader.LoadAndApplyAsync(configService.Object, runtimeConfig, NullLogger.Instance, CancellationToken.None);

            Assert.Equal("https://video.example.com", runtimeConfig.InternetServerUrl);
            Assert.Equal(NetworkTier.Internet, runtimeConfig.ConfiguredNetworkTier);
        }

        [Fact]
        public async Task LoadAndApplyAsync_PropagatesFfmpegBinDirectoryAndReleaseChannel()
        {
            var loadedConfig = new ForensicsConfiguration
            {
                FfmpegBinDirectory = @"C:fmpegin",
                ReleaseChannel = UpdateReleaseChannel.Testing,
            };

            var configService = new Mock<IForensicsConfigurationService>();
            _ = configService.Setup(s => s.LoadConfigurationAsync("", It.IsAny<CancellationToken>()))
                .ReturnsAsync(loadedConfig);

            var runtimeConfig = new ForensicsConfiguration();

            await ConfigurationLoader.LoadAndApplyAsync(configService.Object, runtimeConfig, NullLogger.Instance, CancellationToken.None);

            Assert.Equal(@"C:fmpegin", runtimeConfig.FfmpegBinDirectory);
            Assert.Equal(UpdateReleaseChannel.Testing, runtimeConfig.ReleaseChannel);
        }
    }
}
