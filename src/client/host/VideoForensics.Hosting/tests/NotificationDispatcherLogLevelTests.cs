using Microsoft.Extensions.Logging;

using Moq;

using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Hosting.Tests
{
    /// <summary>A provider that fails is a non-critical side effect: it is logged as a warning and the other providers still send.</summary>
    public class NotificationDispatcherLogLevelTests
    {
        private static NotificationEvent Event() => new(
            EventType: "AccountRemoved",
            TimestampUtc: DateTime.UtcNow,
            OperatorId: null,
            PairedDeviceId: null,
            SourceIp: null,
            Details: "details",
            Audience: NotificationAudience.AdminsOnly,
            Severity: NoticeSeverity.Alert);

        [Fact]
        public async Task DispatchAsync_OneProviderFails_LogsWarningAndStillSendsToTheOthers()
        {
            var failing = new Mock<INotificationProvider>();
            failing.SetupGet(p => p.Name).Returns("failing");
            failing.Setup(p => p.IsEnabledAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
            failing.Setup(p => p.SendAsync(It.IsAny<NotificationEvent>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("smtp down"));

            var healthy = new Mock<INotificationProvider>();
            healthy.SetupGet(p => p.Name).Returns("healthy");
            healthy.Setup(p => p.IsEnabledAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
            healthy.Setup(p => p.SendAsync(It.IsAny<NotificationEvent>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var logger = new Mock<ILogger<NotificationDispatcher>>();
            var notices = new Mock<INoticeRepository>();
            var dispatcher = new NotificationDispatcher(new[] { failing.Object, healthy.Object }, logger.Object, notices.Object);

            await dispatcher.DispatchAsync(Event(), CancellationToken.None);

            healthy.Verify(p => p.SendAsync(It.IsAny<NotificationEvent>(), It.IsAny<CancellationToken>()), Times.Once);
            logger.Verify(l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
            logger.Verify(l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Never);
        }
    }
}
