using Microsoft.Extensions.Logging;

using VideoForensics.Core.Providers.Common.Contracts;

namespace VideoForensics.Client.Hosting.Tests
{
    /// <summary>A provider that fails is a non-critical side effect: it is logged as a warning and the other providers still send.</summary>
    public class NotificationDispatcherLogLevelTests
    {
        private static NotificationEvent Event()
        {
            return new(
            EventType: "AccountRemoved",
            TimestampUtc: DateTime.UtcNow,
            OperatorId: null,
            PairedDeviceId: null,
            SourceIp: null,
            Details: "details",
            Audience: NotificationAudience.AdminsOnly,
            Severity: NoticeSeverity.Alert);
        }

        [Fact]
        public async Task DispatchAsync_OneProviderFails_LogsWarningAndStillSendsToTheOthers()
        {
            var failing = new Mock<INotificationProvider>();
            _ = failing.SetupGet(p => p.Name).Returns("failing");
            _ = failing.Setup(p => p.IsEnabledAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _ = failing.Setup(p => p.SendAsync(It.IsAny<NotificationEvent>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("smtp down"));

            var healthy = new Mock<INotificationProvider>();
            _ = healthy.SetupGet(p => p.Name).Returns("healthy");
            _ = healthy.Setup(p => p.IsEnabledAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _ = healthy.Setup(p => p.SendAsync(It.IsAny<NotificationEvent>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

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
