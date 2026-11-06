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
