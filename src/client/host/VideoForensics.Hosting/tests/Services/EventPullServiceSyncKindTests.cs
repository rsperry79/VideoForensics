namespace VideoForensics.Hosting.Tests.Services
{
    using Moq;
    using VideoForensics.Core.Providers.Common.Contracts;
    using VideoForensics.Data.Common.Contracts;
    using VideoForensics.Hosting.Services;
    using LocationEntity = VideoForensics.Data.Common.Entities.Location;
    using DeviceEntity = VideoForensics.Data.Common.Entities.Device;
    using ProviderAccountEntity = VideoForensics.Data.Common.Entities.ProviderAccount;

    /// <summary>
    /// The sync kind chosen on a manual sync decides which per-device feeds the pull runs: events, device config, both or
    /// neither (snapshots). The date range's end bounds the event pull.
    /// </summary>
    public class EventPullServiceSyncKindTests
    {
        private const string DeviceProviderId = "device-123";
        private readonly Guid _accountId = Guid.NewGuid();
        private readonly Mock<IEventAndConfigService> _events = new();
        private readonly Mock<IDeviceRepository> _devices = new();
        private readonly Mock<ILocationRepository> _locations = new();
        private readonly Mock<IProviderAccountRepository> _accounts = new();
        private readonly Mock<IDeviceDiscoveryService> _discovery = new();
        private readonly SyncRunOptionsHolder _holder = new();
        private readonly Mock<IMediaDownloadService> _media = new();

        public EventPullServiceSyncKindTests()
        {
            // Media is the events feed now: an empty successful download, so the pull runs without counting anything.
            _media.Setup(m => m.DownloadVideosAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DownloadResult(Success: true));
        }

        private EventPullService CreateSut() => new(
            _events.Object,
            _devices.Object,
            _locations.Object,
            _accounts.Object,
            _discovery.Object,
            logger: null,
            syncRunTally: null,
            syncRunOptions: _holder,
            mediaDownloadService: _media.Object,
            mediaRoot: @"C:\media-root",
            minRequestInterval: TimeSpan.Zero);

        private void SetUpOneDevice()
        {
            var account = new ProviderAccountEntity
            {
                Id = _accountId,
                UserId = Guid.NewGuid(),
                ProviderName = "Ring",
                LinkedUtc = DateTime.UtcNow,
                IsActive = true
            };
            _accounts.Setup(r => r.GetAsync(_accountId, It.IsAny<CancellationToken>())).ReturnsAsync(account);

            var location = new LocationEntity { Id = Guid.NewGuid(), ProviderLocationId = "location-123", Name = "Home" };
            var device = new DeviceEntity { Id = Guid.NewGuid(), LocationId = location.Id, ProviderDeviceId = DeviceProviderId, Name = "Front Door", Type = "doorbell" };
            DiscoveryStub.Wire(_accountId, location, new[] { device }, _discovery, _locations, _devices);
        }

        [Fact]
        public async Task EventPull_EventsKind_RunsEventsAndSkipsDeviceConfig()
        {
            SetUpOneDevice();
            _holder.Current = new SyncRunOptions(SyncRunKind.Events, SyncRangeKind.SinceLast, StartUtc: DateTime.UtcNow.AddDays(-1));

            await CreateSut().PullAccountEventsAsync(_accountId, DateTime.UtcNow.AddDays(-1), CancellationToken.None);

            _media.Verify(m => m.DownloadVideosAsync(DeviceProviderId, It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
            _events.Verify(s => s.GetDeviceConfigAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task EventPull_ConfigKind_RunsDeviceConfigAndSkipsEvents()
        {
            SetUpOneDevice();
            _holder.Current = new SyncRunOptions(SyncRunKind.Config, SyncRangeKind.SinceLast);

            await CreateSut().PullAccountEventsAsync(_accountId, null, CancellationToken.None);

            _media.Verify(m => m.DownloadVideosAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
            _events.Verify(s => s.GetDeviceConfigAsync(_accountId, DeviceProviderId, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task EventPull_SnapshotsKind_SkipsEventsAndDeviceConfig()
        {
            SetUpOneDevice();
            _holder.Current = new SyncRunOptions(SyncRunKind.Snapshots, SyncRangeKind.SinceLast);

            await CreateSut().PullAccountEventsAsync(_accountId, null, CancellationToken.None);

            _media.Verify(m => m.DownloadVideosAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
            _events.Verify(s => s.GetDeviceConfigAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task EventPull_CustomEnd_UsesRangeEndAsEventsEnd()
        {
            SetUpOneDevice();
            var start = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
            var end = new DateTime(2026, 10, 2, 0, 0, 0, DateTimeKind.Utc);
            _holder.Current = new SyncRunOptions(SyncRunKind.Events, SyncRangeKind.Custom, StartUtc: start, EndUtc: end);

            await CreateSut().PullAccountEventsAsync(_accountId, start, CancellationToken.None);

            _media.Verify(m => m.DownloadVideosAsync(DeviceProviderId, It.IsAny<string>(), start, end, It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
