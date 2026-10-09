using Moq;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Providers.Common.Contracts;
using VideoForensics.WebApp.Hubs;
using VideoForensics.WebApp.Services;

using Xunit;

namespace VideoForensics.WebApp.Tests
{
    public class WebPushNotificationProviderTests
    {
        private readonly Mock<IPushSubscriptionRepository> _pushRepository = new();
        private readonly Mock<IOperatorNotificationPreferenceRepository> _preferenceRepository = new();
        private readonly WebPushNotificationProvider _provider;

        public WebPushNotificationProviderTests()
        {
            // VAPID keys are never requested by target resolution, so the key provider can be a bare instance.
            var vapidKeyProvider = new VapidKeyProvider(Mock.Of<IAppSettingRepository>());
            _provider = new WebPushNotificationProvider(
                _pushRepository.Object,
                _preferenceRepository.Object,
                vapidKeyProvider,
                Microsoft.Extensions.Logging.Abstractions.NullLogger<WebPushNotificationProvider>.Instance);
        }

        [Fact]
        public async Task ResolveTargetSubscriptionsAsync_AdminsOnlyAudience_CallsListForAdminsAsync()
        {
            _pushRepository
                .Setup(r => r.ListForAdminsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<PushSubscription>());

            _ = await _provider.ResolveTargetSubscriptionsAsync(NotificationAudience.AdminsOnly, CancellationToken.None);

            _pushRepository.Verify(r => r.ListForAdminsAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ResolveTargetSubscriptionsAsync_AdminsOnlyAudience_DoesNotCallListAllAsync()
        {
            _pushRepository
                .Setup(r => r.ListForAdminsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<PushSubscription>());

            _ = await _provider.ResolveTargetSubscriptionsAsync(NotificationAudience.AdminsOnly, CancellationToken.None);

            _pushRepository.Verify(r => r.ListAllAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ResolveTargetSubscriptionsAsync_AllAudience_CallsListAllAsync()
        {
            _pushRepository
                .Setup(r => r.ListAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<PushSubscription>());

            _ = await _provider.ResolveTargetSubscriptionsAsync(NotificationAudience.All, CancellationToken.None);

            _pushRepository.Verify(r => r.ListAllAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task ResolveTargetSubscriptionsAsync_AllAudience_DoesNotCallListForAdminsAsync()
        {
            _pushRepository
                .Setup(r => r.ListAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<PushSubscription>());

            _ = await _provider.ResolveTargetSubscriptionsAsync(NotificationAudience.All, CancellationToken.None);

            _pushRepository.Verify(r => r.ListForAdminsAsync(It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task ResolveTargetSubscriptionsAsync_AllAudience_ReturnsSubscriptionsFromListAllAsync()
        {
            var subscription = new PushSubscription
            {
                Id = Guid.NewGuid(),
                OperatorId = Guid.NewGuid(),
                Endpoint = "https://push.example.com/all-endpoint",
                P256dhKey = "p256dh",
                AuthKey = "auth",
                CreatedUtc = DateTime.UtcNow
            };
            _pushRepository
                .Setup(r => r.ListAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<PushSubscription> { subscription });

            IReadOnlyList<PushSubscription> result = await _provider.ResolveTargetSubscriptionsAsync(NotificationAudience.All, CancellationToken.None);

            Assert.Single(result);
            Assert.Same(subscription, result[0]);
        }

        [Fact]
        public async Task ResolveTargetSubscriptionsAsync_UnknownAudience_ThrowsArgumentOutOfRangeException()
        {
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                _provider.ResolveTargetSubscriptionsAsync((NotificationAudience)99, CancellationToken.None));

            _pushRepository.Verify(r => r.ListAllAsync(It.IsAny<CancellationToken>()), Times.Never);
            _pushRepository.Verify(r => r.ListForAdminsAsync(It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
