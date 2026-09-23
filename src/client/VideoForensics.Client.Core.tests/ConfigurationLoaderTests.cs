using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using VideoForensics.Client.Common.Contracts;

using Xunit;

namespace VideoForensics.Client.Core.Tests
{
    public class ConfigurationLoaderTests
    {
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
    }
}
