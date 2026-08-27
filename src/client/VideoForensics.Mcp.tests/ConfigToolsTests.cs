using Microsoft.Extensions.Logging;
using Moq;
using VideoForensics.Client.Common;
using VideoForensics.Mcp.Tools;
using Xunit;

namespace VideoForensics.Mcp.Tests
{
    public class ConfigToolsTests
    {
        [Fact]
        public void FactoryReset_WithoutConfirm_DoesNothingAndReturnsError()
        {
            var config = new ForensicsConfiguration
            {
                DownloadLocation = Path.Combine(Path.GetTempPath(), "videoforensics-mcp-test-should-not-be-touched")
            };
            var logger = new Mock<ILogger<object>>().Object;

            var result = ConfigTools.FactoryReset(config, logger, confirm: false);

            Assert.Contains("not performed", result, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("confirm=true", result);
            Assert.False(Directory.Exists(config.DownloadLocation), "FactoryReset must not touch the filesystem without confirm=true.");
        }

        [Fact]
        public void FactoryReset_DefaultsConfirmToFalse_WhenOmitted()
        {
            var config = new ForensicsConfiguration();
            var logger = new Mock<ILogger<object>>().Object;

            var result = ConfigTools.FactoryReset(config, logger);

            Assert.Contains("not performed", result, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task SetRetentionDays_RejectsNonPositiveValues()
        {
            var config = new ForensicsConfiguration();
            var mockConfigService = new Mock<IForensicsConfigurationService>();

            var result = await ConfigTools.SetRetentionDays(config, mockConfigService.Object, days: 0, CancellationToken.None);

            Assert.Contains("greater than 0", result);
            mockConfigService.Verify(s => s.SaveConfigurationAsync(It.IsAny<IForensicsConfiguration>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task SetMaxConcurrentDownloads_RejectsZeroOrNegative()
        {
            var config = new ForensicsConfiguration();
            var mockConfigService = new Mock<IForensicsConfigurationService>();

            var result = await ConfigTools.SetMaxConcurrentDownloads(config, mockConfigService.Object, 0, CancellationToken.None);

            Assert.Contains("at least 1", result);
            mockConfigService.Verify(s => s.SaveConfigurationAsync(It.IsAny<IForensicsConfiguration>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task SetReportEnabled_TogglesKnownReportType_AndSaves()
        {
            var config = new ForensicsConfiguration { EnableSignalAnomalyReports = false };
            var mockConfigService = new Mock<IForensicsConfigurationService>();

            var result = await ConfigTools.SetReportEnabled(config, mockConfigService.Object, "SignalAnomaly", true, CancellationToken.None);

            Assert.True(config.EnableSignalAnomalyReports);
            Assert.Contains("enabled", result, StringComparison.OrdinalIgnoreCase);
            mockConfigService.Verify(s => s.SaveConfigurationAsync(config, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SetReportEnabled_RejectsUnknownReportType()
        {
            var config = new ForensicsConfiguration();
            var mockConfigService = new Mock<IForensicsConfigurationService>();

            var result = await ConfigTools.SetReportEnabled(config, mockConfigService.Object, "NotAReportType", true, CancellationToken.None);

            Assert.Contains("Unknown report type", result);
            mockConfigService.Verify(s => s.SaveConfigurationAsync(It.IsAny<IForensicsConfiguration>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
