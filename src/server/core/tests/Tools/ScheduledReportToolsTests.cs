using Microsoft.Extensions.Logging;

using Moq;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Core.Tools;
using VideoForensics.Data.Common.Contracts;

using Xunit;

namespace VideoForensics.Client.Core.Tests.Tools
{
    public class ScheduledReportToolsTests
    {
        private readonly Mock<IForensicsConfigurationService> _configServiceMock = new();
        private readonly ConfigToolsOrchestrator _orchestrator;

        public ScheduledReportToolsTests()
        {
            _orchestrator = new ConfigToolsOrchestrator(
                Mock.Of<ILogger<ConfigToolsOrchestrator>>(),
                _configServiceMock.Object,
                Mock.Of<IAppSettingRepository>());
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(169)]
        public async Task SetScheduledReportIntervalHoursAsync_OutOfRange_Rejected(int hours)
        {
            var config = new ForensicsConfiguration();

            (bool success, string message) = await _orchestrator.SetScheduledReportIntervalHoursAsync(config, hours, CancellationToken.None);

            Assert.False(success);
            Assert.Contains("1 and 168", message);
            Assert.Equal(24, config.ScheduledReportIntervalHours);
            _configServiceMock.Verify(s => s.SaveConfigurationAsync(It.IsAny<IForensicsConfiguration>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task SetScheduledReportIntervalHoursAsync_InRange_Saved()
        {
            var config = new ForensicsConfiguration();

            (bool success, _) = await _orchestrator.SetScheduledReportIntervalHoursAsync(config, 6, CancellationToken.None);

            Assert.True(success);
            Assert.Equal(6, config.ScheduledReportIntervalHours);
            _configServiceMock.Verify(s => s.SaveConfigurationAsync(config, CancellationToken.None), Times.Once);
        }

        [Fact]
        public async Task SetScheduledReportsEnabledAsync_Disabled_Saved()
        {
            var config = new ForensicsConfiguration { EnableScheduledReports = true };

            (bool success, _) = await _orchestrator.SetScheduledReportsEnabledAsync(config, false, CancellationToken.None);

            Assert.True(success);
            Assert.False(config.EnableScheduledReports);
            _configServiceMock.Verify(s => s.SaveConfigurationAsync(config, CancellationToken.None), Times.Once);
        }
    }
}
