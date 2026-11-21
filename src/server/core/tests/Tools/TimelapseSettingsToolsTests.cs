using Microsoft.Extensions.Logging;

using Moq;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Core.Tools;
using VideoForensics.Data.Common.Contracts;

using Xunit;

namespace VideoForensics.Client.Core.Tests.Tools
{
    /// <summary>The services-page time-lapse settings: check interval (minutes) and longest segment (minutes).</summary>
    public class TimelapseSettingsToolsTests
    {
        private readonly Mock<IForensicsConfigurationService> _configServiceMock = new();
        private readonly ConfigToolsOrchestrator _orchestrator;

        public TimelapseSettingsToolsTests()
        {
            _orchestrator = new ConfigToolsOrchestrator(
                Mock.Of<ILogger<ConfigToolsOrchestrator>>(),
                _configServiceMock.Object,
                Mock.Of<IAppSettingRepository>());
        }

        [Theory]
        [InlineData(1)]
        [InlineData(15)]
        [InlineData(1440)]
        public async Task SetTimelapseCheckIntervalMinutesAsync_InRange_Saved(int minutes)
        {
            var config = new ForensicsConfiguration();

            (bool success, _) = await _orchestrator.SetTimelapseCheckIntervalMinutesAsync(config, minutes, CancellationToken.None);

            Assert.True(success);
            Assert.Equal(minutes, config.TimelapseCheckIntervalMinutes);
            _configServiceMock.Verify(s => s.SaveConfigurationAsync(config, CancellationToken.None), Times.Once);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1441)]
        public async Task SetTimelapseCheckIntervalMinutesAsync_OutOfRange_Rejected(int minutes)
        {
            var config = new ForensicsConfiguration();

            (bool success, _) = await _orchestrator.SetTimelapseCheckIntervalMinutesAsync(config, minutes, CancellationToken.None);

            Assert.False(success);
            _configServiceMock.Verify(s => s.SaveConfigurationAsync(It.IsAny<IForensicsConfiguration>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(60)]
        [InlineData(1440)]
        public async Task SetTimelapseMaxSegmentMinutesAsync_InRange_Saved(int minutes)
        {
            var config = new ForensicsConfiguration();

            (bool success, _) = await _orchestrator.SetTimelapseMaxSegmentMinutesAsync(config, minutes, CancellationToken.None);

            Assert.True(success);
            Assert.Equal(minutes, config.TimelapseMaxSegmentMinutes);
            _configServiceMock.Verify(s => s.SaveConfigurationAsync(config, CancellationToken.None), Times.Once);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1441)]
        public async Task SetTimelapseMaxSegmentMinutesAsync_OutOfRange_Rejected(int minutes)
        {
            var config = new ForensicsConfiguration();

            (bool success, _) = await _orchestrator.SetTimelapseMaxSegmentMinutesAsync(config, minutes, CancellationToken.None);

            Assert.False(success);
            _configServiceMock.Verify(s => s.SaveConfigurationAsync(It.IsAny<IForensicsConfiguration>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
