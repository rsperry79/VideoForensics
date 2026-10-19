using Microsoft.Extensions.Logging;

using Moq;

using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Core.Providers.Common.Services;
using VideoForensics.Data.Core.Contracts;
using VideoForensics.Providers.Ring.Exceptions;
using VideoForensics.Providers.Ring.Services;

using Xunit;

namespace VideoForensics.Providers.Ring.Tests
{
    /// <summary>Per-account media request pacing and the rate-limit alert in RingMediaDownloadService.</summary>
    public class RingMediaRequestPacingTests
    {
        private static readonly Guid AccountA = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid AccountB = Guid.Parse("22222222-2222-2222-2222-222222222222");

        private static (RingMediaDownloadService Service, Mock<INotificationDispatcher> Dispatcher) CreateService()
        {
            var dispatcher = new Mock<INotificationDispatcher>();
            var service = new RingMediaDownloadService(
                new Mock<ILogger>().Object,
                new Mock<ISessionProvider>().Object,
                new Mock<IVideoForensicsDataClient>().Object,
                metadataTagger: null,
                notificationDispatcher: dispatcher.Object);
            return (service, dispatcher);
        }

        [Fact]
        public void GetPacerForAccount_DifferentAccounts_ReturnsIndependentPacers()
        {
            (RingMediaDownloadService service, _) = CreateService();

            MediaRequestPacer pacerA = service.GetPacerForAccount(AccountA);
            MediaRequestPacer pacerB = service.GetPacerForAccount(AccountB);

            Assert.NotSame(pacerA, pacerB);
            Assert.Same(pacerA, service.GetPacerForAccount(AccountA));
        }

        [Fact]
        public async Task RecordThrottledAsync_RateLimitOnAccountA_SlowsOnlyAccountA()
        {
            (RingMediaDownloadService service, _) = CreateService();
            MediaRequestPacer pacerA = service.GetPacerForAccount(AccountA);
            MediaRequestPacer pacerB = service.GetPacerForAccount(AccountB);
            TimeSpan baseInterval = pacerB.MinInterval;

            await service.RecordThrottledAsync(AccountA, new ThrottledException { RetryAfter = TimeSpan.FromSeconds(5) }, CancellationToken.None);

            Assert.Equal(baseInterval + TimeSpan.FromSeconds(1), pacerA.MinInterval);
            Assert.Equal(baseInterval, pacerB.MinInterval);
        }

        [Fact]
        public async Task RecordThrottledAsync_TwoRateLimitsInOneIncident_RaisesOneAlert()
        {
            (RingMediaDownloadService service, Mock<INotificationDispatcher> dispatcher) = CreateService();

            await service.RecordThrottledAsync(AccountA, new ThrottledException(), CancellationToken.None);
            await service.RecordThrottledAsync(AccountA, new ThrottledException(), CancellationToken.None);

            dispatcher.Verify(d => d.DispatchAsync(It.IsAny<NotificationEvent>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RecordThrottledAsync_TwoRateLimitsInOneIncident_AddsOneSecondOfSpacing()
        {
            (RingMediaDownloadService service, _) = CreateService();
            MediaRequestPacer pacerA = service.GetPacerForAccount(AccountA);
            TimeSpan baseInterval = pacerA.MinInterval;

            await service.RecordThrottledAsync(AccountA, new ThrottledException(), CancellationToken.None);
            await service.RecordThrottledAsync(AccountA, new ThrottledException(), CancellationToken.None);

            Assert.Equal(baseInterval + TimeSpan.FromSeconds(1), pacerA.MinInterval);
        }

        [Fact]
        public async Task RecordThrottledAsync_RateLimitAfterSuccess_RaisesSecondAlert()
        {
            (RingMediaDownloadService service, Mock<INotificationDispatcher> dispatcher) = CreateService();
            MediaRequestPacer pacerA = service.GetPacerForAccount(AccountA);

            await service.RecordThrottledAsync(AccountA, new ThrottledException(), CancellationToken.None);
            pacerA.MarkRequestSucceeded();
            await service.RecordThrottledAsync(AccountA, new ThrottledException(), CancellationToken.None);

            dispatcher.Verify(d => d.DispatchAsync(It.IsAny<NotificationEvent>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        [Fact]
        public async Task RecordThrottledAsync_RateLimit_AlertNamesProviderAccountNotGuidOrExceptionMessage()
        {
            (RingMediaDownloadService service, Mock<INotificationDispatcher> dispatcher) = CreateService();
            var captured = new List<NotificationEvent>();
            _ = dispatcher.Setup(d => d.DispatchAsync(It.IsAny<NotificationEvent>(), It.IsAny<CancellationToken>()))
                .Callback<NotificationEvent, CancellationToken>((e, _) => captured.Add(e))
                .Returns(Task.CompletedTask);

            await service.RecordThrottledAsync(AccountA, new ThrottledException("secret-detail-xyz"), CancellationToken.None);

            NotificationEvent alert = Assert.Single(captured);
            Assert.Contains("Ring account", alert.Details);
            Assert.DoesNotContain(AccountA.ToString(), alert.Details);
            Assert.DoesNotContain("secret-detail-xyz", alert.Details);
        }

        [Fact]
        public async Task RecordThrottledAsync_HardBan_DoesNotSlowOrAlert()
        {
            (RingMediaDownloadService service, Mock<INotificationDispatcher> dispatcher) = CreateService();
            MediaRequestPacer pacerA = service.GetPacerForAccount(AccountA);
            TimeSpan baseInterval = pacerA.MinInterval;

            await service.RecordThrottledAsync(AccountA, new ThrottledException("ban", isHardBan: true), CancellationToken.None);

            Assert.Equal(baseInterval, pacerA.MinInterval);
            dispatcher.Verify(d => d.DispatchAsync(It.IsAny<NotificationEvent>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task SetRequestPacing_AfterRateLimit_KeepsRateLimitPenalty()
        {
            (RingMediaDownloadService service, _) = CreateService();
            MediaRequestPacer pacerA = service.GetPacerForAccount(AccountA);
            await service.RecordThrottledAsync(AccountA, new ThrottledException(), CancellationToken.None);

            service.SetRequestPacing(1, TimeSpan.FromSeconds(2));

            Assert.Equal(TimeSpan.FromSeconds(3), pacerA.MinInterval);
        }
    }
}
