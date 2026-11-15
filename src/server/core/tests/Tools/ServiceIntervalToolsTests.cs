using Microsoft.Extensions.Logging;

using Moq;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Core.Tools;
using VideoForensics.Data.Common.Contracts;

using Xunit;

namespace VideoForensics.Client.Core.Tests.Tools
{
    public class ServiceIntervalToolsTests
    {
        private readonly Mock<IForensicsConfigurationService> _configServiceMock = new();
        private readonly ConfigToolsOrchestrator _orchestrator;

        public ServiceIntervalToolsTests()
        {
            _orchestrator = new ConfigToolsOrchestrator(
                Mock.Of<ILogger<ConfigToolsOrchestrator>>(),
                _configServiceMock.Object,
                Mock.Of<IAppSettingRepository>());
        }

        [Theory]
        [InlineData("LogPrune", 48)]
        [InlineData("UpdateCheck", 12)]
        [InlineData("MediaBackfill", 1)]
        public async Task SetServiceIntervalHoursAsync_KnownService_InRange_Saved(string service, int hours)
        {
            var config = new ForensicsConfiguration();

            (bool success, _) = await _orchestrator.SetServiceIntervalHoursAsync(config, service, hours, CancellationToken.None);

            Assert.True(success);
            _configServiceMock.Verify(s => s.SaveConfigurationAsync(config, CancellationToken.None), Times.Once);
            Assert.Equal(hours, service switch
            {
                "LogPrune" => config.LogPruneIntervalHours,
                "UpdateCheck" => config.UpdateCheckIntervalHours,
                _ => config.MediaBackfillIntervalHours,
            });
        }

        [Theory]
        [InlineData("Jamming", 15)]
        [InlineData("Codec", 30)]
        [InlineData("Flag", 1440)]
        [InlineData("DeviceHealth", 15)]
        [InlineData("OfflineRepoll", 10)]
        public async Task SetServiceIntervalMinutesAsync_KnownService_InRange_Saved(string service, int minutes)
        {
            var config = new ForensicsConfiguration();

            (bool success, _) = await _orchestrator.SetServiceIntervalMinutesAsync(config, service, minutes, CancellationToken.None);

            Assert.True(success);
            _configServiceMock.Verify(s => s.SaveConfigurationAsync(config, CancellationToken.None), Times.Once);
            Assert.Equal(minutes, service switch
            {
                "Jamming" => config.JammingIntervalMinutes,
                "Codec" => config.CodecIntervalMinutes,
                "DeviceHealth" => config.DeviceHealthIntervalMinutes,
                "OfflineRepoll" => config.OfflineRepollIntervalMinutes,
                _ => config.FlagIntervalMinutes,
            });
        }

        [Fact]
        public void ForensicsConfiguration_BackfillIntervalMinutes_DefaultsTo15()
        {
            var config = new ForensicsConfiguration();

            Assert.Equal(15, config.JammingIntervalMinutes);
            Assert.Equal(15, config.CodecIntervalMinutes);
            Assert.Equal(15, config.FlagIntervalMinutes);
        }

        [Fact]
        public void ForensicsConfiguration_HealthAndSyncIntervals_Defaults()
        {
            var config = new ForensicsConfiguration();

            Assert.Equal(15, config.DeviceHealthIntervalMinutes);
            Assert.Equal(10, config.OfflineRepollIntervalMinutes);
            Assert.Equal(60, config.ScheduledSyncTickSeconds);
        }

        [Theory]
        [InlineData(5)]
        [InlineData(120)]
        [InlineData(3600)]
        public async Task SetScheduledSyncTickSecondsAsync_InRange_Saved(int seconds)
        {
            var config = new ForensicsConfiguration();

            (bool success, _) = await _orchestrator.SetScheduledSyncTickSecondsAsync(config, seconds, CancellationToken.None);

            Assert.True(success);
            Assert.Equal(seconds, config.ScheduledSyncTickSeconds);
            _configServiceMock.Verify(s => s.SaveConfigurationAsync(config, CancellationToken.None), Times.Once);
        }

        [Theory]
        [InlineData(4)]
        [InlineData(3601)]
        public async Task SetScheduledSyncTickSecondsAsync_OutOfRange_Rejected(int seconds)
        {
            var config = new ForensicsConfiguration();

            (bool success, string message) = await _orchestrator.SetScheduledSyncTickSecondsAsync(config, seconds, CancellationToken.None);

            Assert.False(success);
            Assert.Equal("Scheduled sync tick must be between 5 and 3600 seconds", message);
            Assert.Equal(60, config.ScheduledSyncTickSeconds);
            _configServiceMock.Verify(s => s.SaveConfigurationAsync(It.IsAny<IForensicsConfiguration>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1441)]
        public async Task SetServiceIntervalMinutesAsync_OutOfRange_Rejected(int minutes)
        {
            var config = new ForensicsConfiguration();

            (bool success, _) = await _orchestrator.SetServiceIntervalMinutesAsync(config, "Jamming", minutes, CancellationToken.None);

            Assert.False(success);
            Assert.Equal(15, config.JammingIntervalMinutes);
        }

        [Fact]
        public async Task SetServiceIntervalHoursAsync_UnknownService_Rejected()
        {
            var config = new ForensicsConfiguration();

            (bool success, string message) = await _orchestrator.SetServiceIntervalHoursAsync(config, "Nope", 6, CancellationToken.None);

            Assert.False(success);
            Assert.Contains("Unknown service", message);
            _configServiceMock.Verify(s => s.SaveConfigurationAsync(It.IsAny<IForensicsConfiguration>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(169)]
        public async Task SetServiceIntervalHoursAsync_OutOfRange_Rejected(int hours)
        {
            var config = new ForensicsConfiguration();

            (bool success, _) = await _orchestrator.SetServiceIntervalHoursAsync(config, "LogPrune", hours, CancellationToken.None);

            Assert.False(success);
            Assert.Equal(24, config.LogPruneIntervalHours);
        }
    }
}
