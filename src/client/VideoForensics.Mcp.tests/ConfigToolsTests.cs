using Microsoft.Extensions.Logging;
using Moq;
using VideoForensics.Client.Common;
using VideoForensics.Client.Core.Tools;
using VideoForensics.Mcp.Tools;
using Xunit;

namespace VideoForensics.Mcp.Tests
{
    public class ConfigToolsTests
    {
        private readonly Mock<ILogger<ConfigToolsOrchestrator>> _loggerMock;
        private readonly Mock<IForensicsConfigurationService> _configServiceMock;
        private readonly ConfigToolsOrchestrator _orchestrator;

        public ConfigToolsTests()
        {
            _loggerMock = new Mock<ILogger<ConfigToolsOrchestrator>>();
            _configServiceMock = new Mock<IForensicsConfigurationService>();
            _orchestrator = new ConfigToolsOrchestrator(_loggerMock.Object, _configServiceMock.Object);
        }

        [Fact]
        public async Task FactoryReset_WithoutConfirm_DoesNothingAndReturnsError()
        {
            var config = new ForensicsConfiguration();
            var logger = new Mock<ILogger<object>>().Object;

            var result = await ConfigTools.FactoryReset(config, logger, _orchestrator, confirm: false);

            Assert.Contains("not performed", result, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("confirm=true", result);
        }

        [Fact]
        public async Task FactoryReset_DefaultsConfirmToFalse_WhenOmitted()
        {
            var config = new ForensicsConfiguration();
            var logger = new Mock<ILogger<object>>().Object;

            var result = await ConfigTools.FactoryReset(config, logger, _orchestrator);

            Assert.Contains("not performed", result, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task SetRetentionDays_RejectsNonPositiveValues()
        {
            var config = new ForensicsConfiguration();
            var result = await ConfigTools.SetRetentionDays(config, _configServiceMock.Object, _orchestrator, 0, CancellationToken.None);

            Assert.Contains("greater than 0", result);
        }

        [Fact]
        public async Task SetMaxConcurrentDownloads_RejectsZeroOrNegative()
        {
            var config = new ForensicsConfiguration();
            var result = await ConfigTools.SetMaxConcurrentDownloads(config, _configServiceMock.Object, _orchestrator, 0, CancellationToken.None);

            Assert.Contains("at least 1", result);
        }

        [Fact]
        public async Task SetReportEnabled_AcceptsValidReportType()
        {
            var config = new ForensicsConfiguration { EnableSignalAnomalyReports = false };
            var result = await ConfigTools.SetReportEnabled(config, _configServiceMock.Object, _orchestrator, "SignalAnomaly", true, CancellationToken.None);

            Assert.Contains("enabled", result, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task SetReportEnabled_RejectsUnknownReportType()
        {
            var config = new ForensicsConfiguration();
            var result = await ConfigTools.SetReportEnabled(config, _configServiceMock.Object, _orchestrator, "NotAReportType", true, CancellationToken.None);

            Assert.Contains("Unknown report type", result);
        }
    }
}
