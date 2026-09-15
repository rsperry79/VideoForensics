using Microsoft.Extensions.Logging;

using Moq;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.Repositories;

using Xunit;

namespace VideoForensics.Data.Database.Tests
{
    /// <summary>Tests for EventBackfillService, which reconstructs Events from DownloadEvents and MediaItems history.</summary>
    public class EventBackfillServiceTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private EventRepository _eventRepository = null!;
        private DownloadEventRepository _downloadEventRepository = null!;
        private MediaItemRepository _mediaItemRepository = null!;
        private ILoggerFactory _loggerFactory = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            _loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => { });

            _eventRepository = new EventRepository(_fixture.Factory, _loggerFactory.CreateLogger<EventRepository>());
            _downloadEventRepository = new DownloadEventRepository(_fixture.Factory, _loggerFactory.CreateLogger<DownloadEventRepository>());
            _mediaItemRepository = new MediaItemRepository(_fixture.Factory, _loggerFactory.CreateLogger<MediaItemRepository>());
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
            _loggerFactory.Dispose();
        }

        [Fact]
        public async Task EventBackfillService_BackfillFromDownloadEventsAsync_NoDownloadEvents_ReturnsZero()
        {
            var mockLogger = new Mock<ILogger>();

            int result = await EventBackfillService.BackfillFromDownloadEventsAsync(
                _downloadEventRepository,
                _mediaItemRepository,
                _eventRepository,
                mockLogger.Object,
                CancellationToken.None);

            Assert.Equal(0, result);
            mockLogger.Verify(
                l => l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((state, _) => state.ToString().Contains("no DownloadEvents found")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task EventBackfillService_BackfillFromDownloadEventsAsync_SingleDownloadEventNoMediaItem_CreatesEventWithoutHash()
        {
            var deviceId = Guid.NewGuid();
            var downloadEventId = Guid.NewGuid();
            string providerEventId = "ring_evt_001";
            DateTime now = DateTime.UtcNow;

            DownloadEvent downloadEvent = TestDataBuilder.BuildDownloadEvent(deviceId, providerEventId, success: true);
            downloadEvent.Id = downloadEventId;
            downloadEvent.EventOccurredAtUtc = now;
            downloadEvent.DownloadStartedUtc = now.AddSeconds(30);
            downloadEvent.DownloadCompletedUtc = now.AddSeconds(60);

            await _downloadEventRepository.AddAsync(downloadEvent, CancellationToken.None);

            var mockLogger = new Mock<ILogger>();

            int result = await EventBackfillService.BackfillFromDownloadEventsAsync(
                _downloadEventRepository,
                _mediaItemRepository,
                _eventRepository,
                mockLogger.Object,
                CancellationToken.None);

            Assert.Equal(1, result);

            Event? createdEvent = await _eventRepository.GetByProviderEventIdAsync(deviceId, providerEventId, CancellationToken.None);
            Assert.NotNull(createdEvent);
            Assert.Equal(deviceId, createdEvent.DeviceId);
            Assert.Equal(providerEventId, createdEvent.ProviderEventId);
            Assert.Equal(now, createdEvent.OccurredAtUtc);
            Assert.Equal(now.AddSeconds(30), createdEvent.DiscoveredAtUtc);
            Assert.Equal(now.AddSeconds(60), createdEvent.DownloadedAtUtc);
            Assert.Null(createdEvent.EventIntegrityHash);
        }

        [Fact]
        public async Task EventBackfillService_BackfillFromDownloadEventsAsync_SingleDownloadEventWithMediaItem_CreatesEventWithHash()
        {
            var deviceId = Guid.NewGuid();
            var downloadEventId = Guid.NewGuid();
            string providerEventId = "ring_evt_002";
            string mediaItemHash = "abc123def456ghi789jkl012mno345pqr678stu901vwx234yz";
            DateTime now = DateTime.UtcNow;

            DownloadEvent downloadEvent = TestDataBuilder.BuildDownloadEvent(deviceId, providerEventId, success: true);
            downloadEvent.Id = downloadEventId;
            downloadEvent.EventOccurredAtUtc = now;
            downloadEvent.DownloadStartedUtc = now.AddSeconds(30);
            downloadEvent.DownloadCompletedUtc = now.AddSeconds(60);

            await _downloadEventRepository.AddAsync(downloadEvent, CancellationToken.None);

            MediaItem mediaItem = TestDataBuilder.BuildMediaItem(deviceId, downloadEventId, hash: mediaItemHash);
            await _mediaItemRepository.AddAsync(mediaItem, CancellationToken.None);

            var mockLogger = new Mock<ILogger>();

            int result = await EventBackfillService.BackfillFromDownloadEventsAsync(
                _downloadEventRepository,
                _mediaItemRepository,
                _eventRepository,
                mockLogger.Object,
                CancellationToken.None);

            Assert.Equal(1, result);

            Event? createdEvent = await _eventRepository.GetByProviderEventIdAsync(deviceId, providerEventId, CancellationToken.None);
            Assert.NotNull(createdEvent);
            Assert.Equal(mediaItemHash, createdEvent.EventIntegrityHash);
        }

        [Fact]
        public async Task EventBackfillService_BackfillFromDownloadEventsAsync_FailedDownloadEvent_DoesNotSetDownloadedAtUtc()
        {
            var deviceId = Guid.NewGuid();
            var downloadEventId = Guid.NewGuid();
            string providerEventId = "ring_evt_003";
            DateTime now = DateTime.UtcNow;

            DownloadEvent downloadEvent = TestDataBuilder.BuildDownloadEvent(deviceId, providerEventId, success: false);
            downloadEvent.Id = downloadEventId;
            downloadEvent.EventOccurredAtUtc = now;
            downloadEvent.DownloadStartedUtc = now.AddSeconds(30);
            downloadEvent.DownloadCompletedUtc = null;

            await _downloadEventRepository.AddAsync(downloadEvent, CancellationToken.None);

            var mockLogger = new Mock<ILogger>();

            int result = await EventBackfillService.BackfillFromDownloadEventsAsync(
                _downloadEventRepository,
                _mediaItemRepository,
                _eventRepository,
                mockLogger.Object,
                CancellationToken.None);

            Assert.Equal(1, result);

            Event? createdEvent = await _eventRepository.GetByProviderEventIdAsync(deviceId, providerEventId, CancellationToken.None);
            Assert.NotNull(createdEvent);
            Assert.Null(createdEvent.DownloadedAtUtc);
        }

        [Fact]
        public async Task EventBackfillService_BackfillFromDownloadEventsAsync_MultipleDownloadEvents_CreatesMultipleEvents()
        {
            var deviceId = Guid.NewGuid();
            DateTime now = DateTime.UtcNow;

            // Create three download events
            DownloadEvent event1 = TestDataBuilder.BuildDownloadEvent(deviceId, "evt_1", success: true);
            event1.EventOccurredAtUtc = now;
            DownloadEvent event2 = TestDataBuilder.BuildDownloadEvent(deviceId, "evt_2", success: true);
            event2.EventOccurredAtUtc = now.AddHours(1);
            DownloadEvent event3 = TestDataBuilder.BuildDownloadEvent(deviceId, "evt_3", success: true);
            event3.EventOccurredAtUtc = now.AddHours(2);

            await _downloadEventRepository.AddAsync(event1, CancellationToken.None);
            await _downloadEventRepository.AddAsync(event2, CancellationToken.None);
            await _downloadEventRepository.AddAsync(event3, CancellationToken.None);

            var mockLogger = new Mock<ILogger>();

            int result = await EventBackfillService.BackfillFromDownloadEventsAsync(
                _downloadEventRepository,
                _mediaItemRepository,
                _eventRepository,
                mockLogger.Object,
                CancellationToken.None);

            Assert.Equal(3, result);

            Event? createdEvent1 = await _eventRepository.GetByProviderEventIdAsync(deviceId, "evt_1", CancellationToken.None);
            Event? createdEvent2 = await _eventRepository.GetByProviderEventIdAsync(deviceId, "evt_2", CancellationToken.None);
            Event? createdEvent3 = await _eventRepository.GetByProviderEventIdAsync(deviceId, "evt_3", CancellationToken.None);

            Assert.NotNull(createdEvent1);
            Assert.NotNull(createdEvent2);
            Assert.NotNull(createdEvent3);
            Assert.Equal("Motion", createdEvent1.EventType);
            Assert.Equal("Motion", createdEvent2.EventType);
            Assert.Equal("Motion", createdEvent3.EventType);
        }

        [Fact]
        public async Task EventBackfillService_BackfillFromDownloadEventsAsync_EventTypeNull_SetsToUnknown()
        {
            var deviceId = Guid.NewGuid();
            string providerEventId = "ring_evt_004";

            DownloadEvent downloadEvent = TestDataBuilder.BuildDownloadEvent(deviceId, providerEventId, success: true);
            downloadEvent.EventType = null;

            await _downloadEventRepository.AddAsync(downloadEvent, CancellationToken.None);

            var mockLogger = new Mock<ILogger>();

            int result = await EventBackfillService.BackfillFromDownloadEventsAsync(
                _downloadEventRepository,
                _mediaItemRepository,
                _eventRepository,
                mockLogger.Object,
                CancellationToken.None);

            Assert.Equal(1, result);

            Event? createdEvent = await _eventRepository.GetByProviderEventIdAsync(deviceId, providerEventId, CancellationToken.None);
            Assert.NotNull(createdEvent);
            Assert.Equal("unknown", createdEvent.EventType);
        }

        [Fact]
        public async Task EventBackfillService_BackfillFromDownloadEventsAsync_MultipleMediaItemsForSameDownloadEvent_UsesFirstHash()
        {
            var deviceId = Guid.NewGuid();
            var downloadEventId = Guid.NewGuid();
            string providerEventId = "ring_evt_005";
            string hash1 = "hash_001_abc";
            string hash2 = "hash_002_def";

            DownloadEvent downloadEvent = TestDataBuilder.BuildDownloadEvent(deviceId, providerEventId, success: true);
            downloadEvent.Id = downloadEventId;

            await _downloadEventRepository.AddAsync(downloadEvent, CancellationToken.None);

            // Add two media items for the same download event
            MediaItem item1 = TestDataBuilder.BuildMediaItem(deviceId, downloadEventId, hash: hash1);
            MediaItem item2 = TestDataBuilder.BuildMediaItem(deviceId, downloadEventId, hash: hash2);

            await _mediaItemRepository.AddAsync(item1, CancellationToken.None);
            await _mediaItemRepository.AddAsync(item2, CancellationToken.None);

            var mockLogger = new Mock<ILogger>();

            int result = await EventBackfillService.BackfillFromDownloadEventsAsync(
                _downloadEventRepository,
                _mediaItemRepository,
                _eventRepository,
                mockLogger.Object,
                CancellationToken.None);

            Assert.Equal(1, result);

            Event? createdEvent = await _eventRepository.GetByProviderEventIdAsync(deviceId, providerEventId, CancellationToken.None);
            Assert.NotNull(createdEvent);
            // Should be one of the two hashes (the first one encountered in the grouping)
            Assert.True(createdEvent.EventIntegrityHash == hash1 || createdEvent.EventIntegrityHash == hash2);
        }

        [Fact]
        public async Task EventBackfillService_BackfillFromDownloadEventsAsync_CancellationRequested_CancelsBackfill()
        {
            var deviceId = Guid.NewGuid();
            var downloadEventId = Guid.NewGuid();

            DownloadEvent downloadEvent = TestDataBuilder.BuildDownloadEvent(deviceId, "evt_cancel", success: true);
            downloadEvent.Id = downloadEventId;

            await _downloadEventRepository.AddAsync(downloadEvent, CancellationToken.None);

            var mockLogger = new Mock<ILogger>();
            var cts = new CancellationTokenSource();
            cts.Cancel();

            _ = await Assert.ThrowsAsync<OperationCanceledException>(async () =>
                await EventBackfillService.BackfillFromDownloadEventsAsync(
                    _downloadEventRepository,
                    _mediaItemRepository,
                    _eventRepository,
                    mockLogger.Object,
                    cts.Token));
        }

        [Fact]
        public async Task EventBackfillService_BackfillFromDownloadEventsAsync_LogsSuccessMessage()
        {
            var deviceId = Guid.NewGuid();

            DownloadEvent downloadEvent = TestDataBuilder.BuildDownloadEvent(deviceId, "evt_log", success: true);
            await _downloadEventRepository.AddAsync(downloadEvent, CancellationToken.None);

            var mockLogger = new Mock<ILogger>();

            int result = await EventBackfillService.BackfillFromDownloadEventsAsync(
                _downloadEventRepository,
                _mediaItemRepository,
                _eventRepository,
                mockLogger.Object,
                CancellationToken.None);

            Assert.Equal(1, result);
            mockLogger.Verify(
                l => l.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((state, _) => state.ToString().Contains("reconstructed") && state.ToString().Contains("1")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task EventBackfillService_BackfillFromDownloadEventsAsync_MediaItemWithoutDownloadEventId_Ignored()
        {
            var deviceId = Guid.NewGuid();
            var downloadEventId = Guid.NewGuid();
            string providerEventId = "ring_evt_006";

            DownloadEvent downloadEvent = TestDataBuilder.BuildDownloadEvent(deviceId, providerEventId, success: true);
            downloadEvent.Id = downloadEventId;

            await _downloadEventRepository.AddAsync(downloadEvent, CancellationToken.None);

            // Add a media item without a DownloadEventId (not associated with any download event)
            MediaItem orphanItem = TestDataBuilder.BuildMediaItem(deviceId, downloadEventId: null, hash: "orphan_hash");
            await _mediaItemRepository.AddAsync(orphanItem, CancellationToken.None);

            var mockLogger = new Mock<ILogger>();

            int result = await EventBackfillService.BackfillFromDownloadEventsAsync(
                _downloadEventRepository,
                _mediaItemRepository,
                _eventRepository,
                mockLogger.Object,
                CancellationToken.None);

            Assert.Equal(1, result);

            Event? createdEvent = await _eventRepository.GetByProviderEventIdAsync(deviceId, providerEventId, CancellationToken.None);
            Assert.NotNull(createdEvent);
            // Should not have a hash since the orphan media item has no DownloadEventId
            Assert.Null(createdEvent.EventIntegrityHash);
        }

        [Fact]
        public async Task EventBackfillService_BackfillFromDownloadEventsAsync_MixedSuccessAndFailedDownloads_BackfillsAll()
        {
            var deviceId = Guid.NewGuid();
            DateTime now = DateTime.UtcNow;

            // Successful download
            DownloadEvent successEvent = TestDataBuilder.BuildDownloadEvent(deviceId, "evt_success", success: true);
            successEvent.EventOccurredAtUtc = now;
            successEvent.DownloadStartedUtc = now.AddSeconds(10);
            successEvent.DownloadCompletedUtc = now.AddSeconds(20);

            // Failed download
            DownloadEvent failedEvent = TestDataBuilder.BuildDownloadEvent(deviceId, "evt_failed", success: false);
            failedEvent.EventOccurredAtUtc = now.AddHours(1);
            failedEvent.DownloadStartedUtc = now.AddHours(1).AddSeconds(10);
            failedEvent.DownloadCompletedUtc = null;

            await _downloadEventRepository.AddAsync(successEvent, CancellationToken.None);
            await _downloadEventRepository.AddAsync(failedEvent, CancellationToken.None);

            var mockLogger = new Mock<ILogger>();

            int result = await EventBackfillService.BackfillFromDownloadEventsAsync(
                _downloadEventRepository,
                _mediaItemRepository,
                _eventRepository,
                mockLogger.Object,
                CancellationToken.None);

            Assert.Equal(2, result);

            Event? successCreated = await _eventRepository.GetByProviderEventIdAsync(deviceId, "evt_success", CancellationToken.None);
            Event? failureCreated = await _eventRepository.GetByProviderEventIdAsync(deviceId, "evt_failed", CancellationToken.None);

            Assert.NotNull(successCreated);
            Assert.NotNull(failureCreated);
            _ = Assert.NotNull(successCreated.DownloadedAtUtc);
            Assert.Null(failureCreated.DownloadedAtUtc);
        }
    }
}
