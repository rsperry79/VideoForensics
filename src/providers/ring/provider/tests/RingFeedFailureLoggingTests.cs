
using Microsoft.Extensions.Logging;

using Moq;

using VideoForensics.Providers.Ring.Exceptions;
using VideoForensics.Providers.Ring.Services;

using Xunit;

namespace VideoForensics.Providers.Ring.Tests
{
    /// <summary>
    /// A throttle during a discovery feed is a handled state, so it is logged as a warning with its message only. An error with a
    /// stack trace is reserved for failures nobody expected.
    /// </summary>
    public class RingFeedFailureLoggingTests
    {
        [Fact]
        public void LogFeedFailure_Throttled_LogsWarningWithoutAnError()
        {
            var logger = new Mock<ILogger<RingDeviceDiscoveryService>>();

            RingDeviceDiscoveryService.LogFeedFailure(logger.Object, "video search", new ThrottledException("Ring has rate-limited this account"));

            logger.Verify(l => l.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception?>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Never);
            logger.Verify(l => l.Log(LogLevel.Warning, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception?>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
        }

        [Fact]
        public void LogFeedFailure_OtherFailure_LogsErrorWithTheException()
        {
            var logger = new Mock<ILogger<RingDeviceDiscoveryService>>();

            RingDeviceDiscoveryService.LogFeedFailure(logger.Object, "video search", new InvalidOperationException("boom"));

            logger.Verify(l => l.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception?>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
        }
    }
}
