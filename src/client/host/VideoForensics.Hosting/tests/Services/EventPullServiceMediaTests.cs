namespace VideoForensics.Hosting.Tests.Services
{
    using VideoForensics.Core.Providers.Common.Contracts;
    using LocationEntity = VideoForensics.Data.Common.Entities.Location;
    using DeviceEntity = VideoForensics.Data.Common.Entities.Device;
    using ProviderAccountEntity = VideoForensics.Data.Common.Entities.ProviderAccount;

    /// <summary>
    /// Events sync counts only media that was actually pulled, pulls the full requested range, paces downloads,
    /// and moves last-pull timestamps only when something was pulled.
    /// </summary>
    public class EventPullServiceMediaTests
    {
        private const string DeviceId = "device-123";
        private const string LocationId = "location-123";
        private const string MediaRoot = "C:\\media-root";

        /// <summary>A window that fits in one 30-day batch, so each count test sees exactly one batch.</summary>
        private static DateTime Recent => DateTime.UtcNow.AddDays(-10);

        private readonly Guid _accountId = Guid.NewGuid();
        private readonly LocationEntity _location = new() { Id = Guid.NewGuid(), ProviderLocationId = LocationId, Name = "Home" };
        private readonly DeviceEntity _device;
        private readonly ProviderAccountEntity _account;
        private readonly Mock<IEventAndConfigService> _eventAndConfig = new();
        private readonly Mock<IDeviceRepository> _deviceRepository = new();
        private readonly Mock<ILocationRepository> _locationRepository = new();
        private readonly Mock<IProviderAccountRepository> _accountRepository = new();
        private readonly Mock<IDeviceDiscoveryService> _discovery = new();
        private readonly Mock<IMediaDownloadService> _media = new();
        private readonly SyncRunTally _tally = new();

        public EventPullServiceMediaTests()
        {
            _device = new DeviceEntity { Id = Guid.NewGuid(), LocationId = _location.Id, ProviderDeviceId = DeviceId, Name = "Front Door", Type = "doorbell" };
            _account = new ProviderAccountEntity
            {
                Id = _accountId,
                UserId = Guid.NewGuid(),
                ProviderName = "Ring",
                LinkedUtc = DateTime.UtcNow.AddDays(-10),
                LastSuccessfulAuthUtc = DateTime.UtcNow.AddDays(-1),
                IsActive = true
            };

            _accountRepository.Setup(r => r.GetAsync(_accountId, It.IsAny<CancellationToken>())).ReturnsAsync(_account);
            DiscoveryStub.Wire(_accountId, _location, new[] { _device }, _discovery, _locationRepository, _deviceRepository);
        }

        private EventPullService CreateSut() => new(
            _eventAndConfig.Object,
            _deviceRepository.Object,
            _locationRepository.Object,
            _accountRepository.Object,
            _discovery.Object,
            logger: null,
            syncRunTally: _tally,
            syncRunOptions: null,
            mediaDownloadService: _media.Object,
            mediaRoot: MediaRoot,
            minRequestInterval: TimeSpan.FromSeconds(1));

        private void SetDownloadResult(DownloadResult result)
        {
            _media.Setup(m => m.DownloadVideosAsync(DeviceId, It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), LocationId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(result);
        }

        [Fact]
        public async Task PullAccountEventsAsync_DownloadSucceeds_CountsEventsPulled()
        {
            SetDownloadResult(new DownloadResult(Success: true, FilesDownloaded: 2, FilesMatched: 2));

            await CreateSut().PullAccountEventsAsync(_accountId, fromTimestampUtc: Recent, CancellationToken.None);

            SyncRunTotals totals = _tally.Snapshot();
            Assert.Equal(2, totals.EventsSynced);
            Assert.Equal(0, totals.EventsNotPulled);
        }

        [Fact]
        public async Task PullAccountEventsAsync_EventWithNoMedia_CountsEventsNotPulledNotSynced()
        {
            // Ring returns 404 for the recording: the event matched but no media was written.
            SetDownloadResult(new DownloadResult(Success: true, FilesDownloaded: 0, FilesMatched: 1, SkipReason: "1 item failed to download"));

            await CreateSut().PullAccountEventsAsync(_accountId, fromTimestampUtc: Recent, CancellationToken.None);

            SyncRunTotals totals = _tally.Snapshot();
            Assert.Equal(0, totals.EventsSynced);
            Assert.Equal(1, totals.EventsNotPulled);
        }

        [Fact]
        public async Task PullAccountEventsAsync_MediaAlreadyOnDisk_CountsAsPulledAndReportsAlreadyPresentSeparately()
        {
            SetDownloadResult(new DownloadResult(Success: true, FilesDownloaded: 2, FilesMatched: 2, FilesAlreadyPresent: 2));

            await CreateSut().PullAccountEventsAsync(_accountId, fromTimestampUtc: Recent, CancellationToken.None);

            SyncRunTotals totals = _tally.Snapshot();
            Assert.Equal(2, totals.EventsSynced);
            Assert.Equal(2, totals.EventsAlreadyPresent);
        }

        [Fact]
        public async Task PullAccountEventsAsync_RunPulledNothing_LeavesLastPullTimestampsUnchanged()
        {
            DateTime? devicePullBefore = _device.LastSuccessfulPullAtUtc;
            DateTime? accountAuthBefore = _account.LastSuccessfulAuthUtc;
            SetDownloadResult(new DownloadResult(Success: true, FilesDownloaded: 0, FilesMatched: 0));

            await CreateSut().PullAccountEventsAsync(_accountId, fromTimestampUtc: Recent, CancellationToken.None);

            Assert.Equal(devicePullBefore, _device.LastSuccessfulPullAtUtc);
            Assert.Equal(accountAuthBefore, _account.LastSuccessfulAuthUtc);
        }

        [Fact]
        public async Task PullAccountEventsAsync_AtLeastOneEventPulled_SetsDeviceLastSuccessfulPull()
        {
            SetDownloadResult(new DownloadResult(Success: true, FilesDownloaded: 1, FilesMatched: 1));

            await CreateSut().PullAccountEventsAsync(_accountId, fromTimestampUtc: Recent, CancellationToken.None);

            Assert.NotNull(_device.LastSuccessfulPullAtUtc);
            Assert.True(_account.LastSuccessfulAuthUtc > DateTime.UtcNow.AddMinutes(-1));
        }

        [Fact]
        public async Task PullAccountEventsAsync_OnlyAlreadyPresentFiles_DoesNotSetDeviceLastSuccessfulPull()
        {
            // Nothing was downloaded in this run, so the last-pull timestamp must not move.
            SetDownloadResult(new DownloadResult(Success: true, FilesDownloaded: 2, FilesMatched: 2, FilesAlreadyPresent: 2));

            await CreateSut().PullAccountEventsAsync(_accountId, fromTimestampUtc: Recent, CancellationToken.None);

            Assert.Null(_device.LastSuccessfulPullAtUtc);
        }

        [Fact]
        public async Task PullAccountEventsAsync_InitialPull_PullsOneYearInThirtyDayBatches()
        {
            SetDownloadResult(new DownloadResult(Success: true, FilesDownloaded: 0, FilesMatched: 0));

            _account.LastSuccessfulAuthUtc = null; // no watermark yet: this is the initial backfill
            await CreateSut().PullAccountEventsAsync(_accountId, fromTimestampUtc: null, CancellationToken.None);

            _media.Verify(m => m.DownloadVideosAsync(DeviceId, It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), LocationId, It.IsAny<CancellationToken>()), Times.Exactly(13));
            _media.Verify(m => m.DownloadVideosAsync(DeviceId, It.IsAny<string>(), It.Is<DateTime>(s => Math.Abs((s - DateTime.UtcNow.AddYears(-1)).TotalMinutes) < 5), It.IsAny<DateTime>(), LocationId, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task PullAccountEventsAsync_NormalSync_StartsFromAccountWatermark()
        {
            DateTime watermark = DateTime.UtcNow.AddDays(-3);
            _account.LastSuccessfulAuthUtc = watermark;
            SetDownloadResult(new DownloadResult(Success: true, FilesDownloaded: 0, FilesMatched: 0));

            await CreateSut().PullAccountEventsAsync(_accountId, fromTimestampUtc: null, CancellationToken.None);

            _media.Verify(m => m.DownloadVideosAsync(DeviceId, It.IsAny<string>(), watermark, It.IsAny<DateTime>(), LocationId, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task PullAccountEventsAsync_InitialPullWithMedia_AdvancesAccountWatermarkAfterEachBatch()
        {
            SetDownloadResult(new DownloadResult(Success: true, FilesDownloaded: 1, FilesMatched: 1));

            _account.LastSuccessfulAuthUtc = null; // no watermark yet: this is the initial backfill
            await CreateSut().PullAccountEventsAsync(_accountId, fromTimestampUtc: null, CancellationToken.None);

            // One save per batch: the watermark is written as each 30-day batch completes.
            _accountRepository.Verify(r => r.UpdateAsync(It.IsAny<ProviderAccountEntity>(), It.IsAny<CancellationToken>()), Times.AtLeast(13));
        }

        [Fact]
        public async Task PullAccountEventsAsync_AnyRun_SetsOneInFlightPacingOnDownloadService()
        {
            SetDownloadResult(new DownloadResult(Success: true, FilesDownloaded: 0, FilesMatched: 0));

            await CreateSut().PullAccountEventsAsync(_accountId, fromTimestampUtc: Recent, CancellationToken.None);

            _media.Verify(m => m.SetRequestPacing(1, TimeSpan.FromSeconds(1)), Times.Once);
        }

        [Fact]
        public async Task PullAccountEventsAsync_DownloadFails_RecordsFeedFailureAndNotPulledCount()
        {
            _media.Setup(m => m.GetMatchedEventCountAsync(DeviceId, It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(3);
            _media.Setup(m => m.DownloadVideosAsync(DeviceId, It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), LocationId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DownloadResult(Success: false, ErrorMessage: "not authenticated"));

            await CreateSut().PullAccountEventsAsync(_accountId, fromTimestampUtc: Recent, CancellationToken.None);

            SyncRunTotals totals = _tally.Snapshot();
            Assert.Equal(0, totals.EventsSynced);
            Assert.Equal(3, totals.EventsNotPulled);
            Assert.Equal(1, totals.FeedsFailed);
        }

        [Fact]
        public async Task PullAccountEventsAsync_RerunAfterInterruption_SkipsPulledEventsAndCountsThemAsAlreadyPresent()
        {
            // Run 1 pulled one of three events, then was interrupted. Run 2 finds that event on disk and pulls the rest.
            SetDownloadResult(new DownloadResult(Success: true, FilesDownloaded: 1, FilesMatched: 3, SkipReason: "cancelled"));
            await CreateSut().PullAccountEventsAsync(_accountId, fromTimestampUtc: Recent, CancellationToken.None);
            Assert.Equal(1, _tally.Snapshot().EventsSynced);

            _tally.Reset();
            SetDownloadResult(new DownloadResult(Success: true, FilesDownloaded: 3, FilesMatched: 3, FilesAlreadyPresent: 1));
            await CreateSut().PullAccountEventsAsync(_accountId, fromTimestampUtc: Recent, CancellationToken.None);

            SyncRunTotals totals = _tally.Snapshot();
            Assert.Equal(3, totals.EventsSynced);
            Assert.Equal(1, totals.EventsAlreadyPresent);
            Assert.Equal(0, totals.EventsNotPulled);
        }
    }
}
