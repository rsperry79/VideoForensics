using VideoForensics.Client.Common.Contracts;
using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Contracts;

using Microsoft.Extensions.Logging.Abstractions;

using ProviderAccountEntity = VideoForensics.Data.Common.Entities.ProviderAccount;

namespace VideoForensics.Client.Hosting.Tests.Services
{
    /// <summary>
    /// Events still awaiting their ding id form a backfill queue. Each pull first retries that queue in one batch per device,
    /// counts a failed pass for each event still waiting, and holds the account's pull while any event has passes left.
    /// </summary>
    public class EventPullServiceBackfillQueueTests
    {
        private readonly Guid _accountId = Guid.NewGuid();
        private readonly Guid _locationId = Guid.NewGuid();
        private readonly Mock<IEventAndConfigService> _events = new();
        private readonly Mock<IDeviceRepository> _devices = new();
        private readonly Mock<ILocationRepository> _locations = new();
        private readonly Mock<IProviderAccountRepository> _accounts = new();
        private readonly Mock<IDeviceDiscoveryService> _discovery = new();
        private readonly Mock<IMediaDownloadService> _media = new();
        private readonly Mock<IEventRepository> _eventRepository = new();
        private readonly ProviderAccountEntity _account;
        private readonly VideoForensics.Data.Common.Entities.Device _device;

        public EventPullServiceBackfillQueueTests()
        {
            _account = new ProviderAccountEntity { Id = _accountId, UserId = Guid.NewGuid(), ProviderName = "Ring", LinkedUtc = DateTime.UtcNow, IsActive = true };
            _device = new VideoForensics.Data.Common.Entities.Device { Id = Guid.NewGuid(), LocationId = _locationId, ProviderDeviceId = "dev-1", Name = "Front Door", Type = "doorbell" };
            var location = new VideoForensics.Data.Common.Entities.Location { Id = _locationId, ProviderLocationId = "location-123", Name = "Home" };

            _ = _accounts.Setup(r => r.GetAsync(_accountId, It.IsAny<CancellationToken>())).ReturnsAsync(_account);
            DiscoveryStub.Wire(_accountId, location, new[] { _device }, _discovery, _locations, _devices);
            _ = _events.Setup(e => e.GetEventsAsync(_accountId, It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<DeviceEvent>());
            _ = _media.Setup(m => m.DownloadVideosAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DownloadResult(Success: true));
        }

        private EventPullService BuildSut()
        {
            return new EventPullService(_events.Object, _devices.Object, _locations.Object, _accounts.Object, NullLogger<EventPullService>.Instance, _media.Object, events: _eventRepository.Object);
        }

        private VideoForensics.Data.Common.Entities.Event Queued(int attempts = 0)
        {
            return new VideoForensics.Data.Common.Entities.Event
            {
                Id = Guid.NewGuid(),
                DeviceId = _device.Id,
                ProviderEventId = Guid.NewGuid().ToString("N"),
                EventType = "motion",
                OccurredAtUtc = DateTime.UtcNow.AddDays(-2),
                DiscoveredAtUtc = DateTime.UtcNow,
                AwaitingDingId = true,
                DownloadAttempts = attempts,
            };
        }

        [Fact]
        public async Task PullAccountEventsAsync_EventStillAwaitingItsDingId_HoldsThePullAndSaysWhy()
        {
            VideoForensics.Data.Common.Entities.Event queued = Queued();
            _ = _eventRepository.Setup(r => r.ListAwaitingDingIdAsync(_device.Id, 3, It.IsAny<CancellationToken>())).ReturnsAsync(new List<VideoForensics.Data.Common.Entities.Event> { queued });

            _ = await BuildSut().PullAccountEventsAsync(_accountId, null, null, CancellationToken.None);

            _events.Verify(e => e.GetEventsAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
            Assert.Contains("waiting for their string ding id", _account.LastErrorMessage);
        }

        [Fact]
        public async Task PullAccountEventsAsync_QueuedEventsOfOneDevice_RetriedInOneBatchAndEachCountsAPass()
        {
            VideoForensics.Data.Common.Entities.Event first = Queued();
            VideoForensics.Data.Common.Entities.Event second = Queued();
            _ = _eventRepository.Setup(r => r.ListAwaitingDingIdAsync(_device.Id, 3, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<VideoForensics.Data.Common.Entities.Event> { first, second });

            _ = await BuildSut().PullAccountEventsAsync(_accountId, null, null, CancellationToken.None);

            _media.Verify(m => m.DownloadVideosAsync("dev-1", It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
            _eventRepository.Verify(r => r.RecordRetryPassAsync(first.Id, 3, It.IsAny<CancellationToken>()), Times.Once);
            _eventRepository.Verify(r => r.RecordRetryPassAsync(second.Id, 3, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task PullAccountEventsAsync_BatchClearsTheQueue_PullGoesAhead()
        {
            VideoForensics.Data.Common.Entities.Event queued = Queued();
            _ = _eventRepository.SetupSequence(r => r.ListAwaitingDingIdAsync(_device.Id, 3, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<VideoForensics.Data.Common.Entities.Event> { queued })
                .ReturnsAsync(new List<VideoForensics.Data.Common.Entities.Event>());

            _ = await BuildSut().PullAccountEventsAsync(_accountId, null, null, CancellationToken.None);

            _eventRepository.Verify(r => r.RecordRetryPassAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
            _events.Verify(e => e.GetEventsAsync(_accountId, "dev-1", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        }
    }
}
