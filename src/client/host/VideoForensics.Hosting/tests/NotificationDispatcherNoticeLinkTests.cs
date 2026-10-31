using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

using Microsoft.Extensions.Logging;

using Xunit;

namespace VideoForensics.Client.Hosting.Tests
{
    /// <summary>The stored notice keeps the event's relative link, so the Notices page can open it.</summary>
    public class NotificationDispatcherNoticeLinkTests
    {
        [Fact]
        public async Task DispatchAsync_EventWithActionPath_StoresTheLinkOnTheNotice()
        {
            var notices = new Mock<INoticeRepository>();
            var dispatcher = new NotificationDispatcher(
                Array.Empty<INotificationProvider>(),
                new Mock<ILogger<NotificationDispatcher>>().Object,
                notices.Object);
            var notificationEvent = new NotificationEvent(
                EventType: "DeviceOffline",
                TimestampUtc: DateTime.UtcNow,
                OperatorId: null,
                PairedDeviceId: null,
                SourceIp: null,
                Details: "Front Door went offline",
                Audience: NotificationAudience.AdminsOnly,
                Severity: NoticeSeverity.Alert,
                ActionPath: "/accounts/1/sync");

            await dispatcher.DispatchAsync(notificationEvent, CancellationToken.None);

            notices.Verify(r => r.AddAsync(It.Is<Notice>(n => n.ActionPath == "/accounts/1/sync"), It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
