using Microsoft.Extensions.Logging;

using Moq;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Core.Tools;
using VideoForensics.Data.Common.Contracts;

using Xunit;

namespace VideoForensics.Client.Core.Tests.Tools
{
    /// <summary>The services-page time-lapse settings: roll-up interval (minutes) and playback length (seconds).</summary>
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
        public async Task SetTimelapseRollupMinutesAsync_InRange_Saved(int minutes)
        {
            var config = new ForensicsConfiguration();

            (bool success, _) = await _orchestrator.SetTimelapseRollupMinutesAsync(config, minutes, CancellationToken.None);

            Assert.True(success);
            Assert.Equal(minutes, config.TimelapseRollupIntervalMinutes);
            _configServiceMock.Verify(s => s.SaveConfigurationAsync(config, CancellationToken.None), Times.Once);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1441)]
        public async Task SetTimelapseRollupMinutesAsync_OutOfRange_Rejected(int minutes)
        {
            var config = new ForensicsConfiguration();

            (bool success, _) = await _orchestrator.SetTimelapseRollupMinutesAsync(config, minutes, CancellationToken.None);

            Assert.False(success);
            _configServiceMock.Verify(s => s.SaveConfigurationAsync(It.IsAny<IForensicsConfiguration>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(30)]
        [InlineData(600)]
        public async Task SetTimelapsePlaybackSecondsAsync_InRange_Saved(int seconds)
        {
            var config = new ForensicsConfiguration();

            (bool success, _) = await _orchestrator.SetTimelapsePlaybackSecondsAsync(config, seconds, CancellationToken.None);

            Assert.True(success);
            Assert.Equal(seconds, config.TimelapsePlaybackSeconds);
            _configServiceMock.Verify(s => s.SaveConfigurationAsync(config, CancellationToken.None), Times.Once);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(601)]
        public async Task SetTimelapsePlaybackSecondsAsync_OutOfRange_Rejected(int seconds)
        {
            var config = new ForensicsConfiguration();

            (bool success, _) = await _orchestrator.SetTimelapsePlaybackSecondsAsync(config, seconds, CancellationToken.None);

            Assert.False(success);
            _configServiceMock.Verify(s => s.SaveConfigurationAsync(It.IsAny<IForensicsConfiguration>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
