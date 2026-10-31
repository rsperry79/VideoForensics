using Microsoft.Extensions.Logging;

using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Contracts;

using Xunit;

namespace VideoForensics.Client.Hosting.Tests
{
    /// <summary>A raised notice reaches the Notices page's feed, so an open page can reload.</summary>
    public class NoticeFeedTests
    {
        private static NotificationEvent Event() =>
            new("DeviceOffline", DateTime.UtcNow, null, null, null, "Front Door went offline", Severity: NoticeSeverity.Alert);

        [Fact]
        public void LocalNoticeFeed_Publish_DeliversToSubscribers()
        {
            using var feed = new LocalNoticeFeed();
            var received = new List<NotificationEvent>();
            using IDisposable subscription = feed.NewNotices.Subscribe(received.Add);

            feed.Publish(Event());

            Assert.Single(received);
        }

        [Fact]
        public async Task DispatchAsync_SavedNotice_PublishesToTheLocalFeed()
        {
            var feed = new LocalNoticeFeed();
            var received = new List<NotificationEvent>();
            using IDisposable subscription = feed.NewNotices.Subscribe(received.Add);
            var dispatcher = new NotificationDispatcher(
                Array.Empty<INotificationProvider>(),
                new Mock<ILogger<NotificationDispatcher>>().Object,
                new Mock<INoticeRepository>().Object,
                feed);

            await dispatcher.DispatchAsync(Event(), CancellationToken.None);

            Assert.Single(received);
        }
    }
}
