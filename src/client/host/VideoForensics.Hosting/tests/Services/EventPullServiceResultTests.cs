namespace VideoForensics.Hosting.Tests.Services
{
    using VideoForensics.Core.Providers.Common.Contracts;
    using ProviderAccountEntity = VideoForensics.Data.Common.Entities.ProviderAccount;

    /// <summary>The pull reports what it synced so a sync run can record real counts.</summary>
    public class EventPullServiceResultTests
    {
        private readonly Mock<IEventAndConfigService> _events = new();
        private readonly Mock<IDeviceRepository> _devices = new();
        private readonly Mock<ILocationRepository> _locations = new();
        private readonly Mock<IProviderAccountRepository> _accounts = new();
        private readonly Mock<IDeviceDiscoveryService> _discovery = new();
        private readonly Guid _accountId = Guid.NewGuid();

        private EventPullService Sut() => new(_events.Object, _devices.Object, _locations.Object, _accounts.Object, _discovery.Object);

        private void SetupAccount() => _accounts
            .Setup(r => r.GetAsync(_accountId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderAccountEntity { Id = _accountId, UserId = Guid.NewGuid(), ProviderName = "Ring", LinkedUtc = DateTime.UtcNow, IsActive = true });

        private static DeviceEntity Device(Guid locationId, string id) => new()
        {
            Id = Guid.NewGuid(),
            LocationId = locationId,
            ProviderDeviceId = id,
            Name = id,
            Type = "doorbell"
        };

        private static List<DeviceEvent> Events(string deviceId, int count) =>
            Enumerable.Range(0, count).Select(i => new DeviceEvent($"{deviceId}-{i}", deviceId, "motion", DateTime.UtcNow.AddMinutes(-i))).ToList();

        private static DeviceConfig Config(string deviceId) => new(deviceId, true, 50, "motion");

        [Fact]
        public async Task PullAccountEventsAsync_TwoDevicesWithEvents_ReturnsCounts()
        {
            SetupAccount();
            var location = new LocationEntity { Id = Guid.NewGuid(), ProviderLocationId = "loc-1", Name = "Home" };
            DeviceEntity a = Device(location.Id, "dev-a");
            DeviceEntity b = Device(location.Id, "dev-b");
            DiscoveryStub.Wire(_accountId, location, new[] { a, b }, _discovery, _locations, _devices);
            _events.Setup(s => s.GetEventsAsync(_accountId, "dev-a", It.IsAny<DateTime>(), It.IsAny<DateTime>(), null, It.IsAny<CancellationToken>())).ReturnsAsync(Events("dev-a", 2));
            _events.Setup(s => s.GetEventsAsync(_accountId, "dev-b", It.IsAny<DateTime>(), It.IsAny<DateTime>(), null, It.IsAny<CancellationToken>())).ReturnsAsync(Events("dev-b", 1));
            _events.Setup(s => s.GetDeviceConfigAsync(_accountId, "dev-a", It.IsAny<CancellationToken>())).ReturnsAsync(Config("dev-a"));
            _events.Setup(s => s.GetDeviceConfigAsync(_accountId, "dev-b", It.IsAny<CancellationToken>())).ReturnsAsync(Config("dev-b"));

            EventPullResult result = await Sut().PullAccountEventsAsync(_accountId, null, null, CancellationToken.None);

            Assert.Equal(new EventPullResult(DevicesSynced: 2, LocationsSynced: 1, EventsSynced: 3, ConfigAreasSynced: 2, FeedsFailed: 0), result);
        }

        [Fact]
        public async Task PullAccountEventsAsync_OneDeviceFeedFails_ReturnsPartialCountsAndFeedsFailed()
        {
            SetupAccount();
            var location = new LocationEntity { Id = Guid.NewGuid(), ProviderLocationId = "loc-1", Name = "Home" };
            DeviceEntity a = Device(location.Id, "dev-a");
            DeviceEntity b = Device(location.Id, "dev-b");
            DiscoveryStub.Wire(_accountId, location, new[] { a, b }, _discovery, _locations, _devices);
            _events.Setup(s => s.GetEventsAsync(_accountId, "dev-a", It.IsAny<DateTime>(), It.IsAny<DateTime>(), null, It.IsAny<CancellationToken>())).ReturnsAsync(Events("dev-a", 2));
            _events.Setup(s => s.GetEventsAsync(_accountId, "dev-b", It.IsAny<DateTime>(), It.IsAny<DateTime>(), null, It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("boom"));
            _events.Setup(s => s.GetDeviceConfigAsync(_accountId, "dev-a", It.IsAny<CancellationToken>())).ReturnsAsync(Config("dev-a"));

            EventPullResult result = await Sut().PullAccountEventsAsync(_accountId, null, null, CancellationToken.None);

            Assert.Equal(new EventPullResult(DevicesSynced: 2, LocationsSynced: 1, EventsSynced: 2, ConfigAreasSynced: 1, FeedsFailed: 1), result);
        }

        [Fact]
        public async Task PullAccountEventsAsync_AccountNotFound_ReturnsEmptyResult()
        {
            _accounts.Setup(r => r.GetAsync(_accountId, It.IsAny<CancellationToken>())).ReturnsAsync((ProviderAccountEntity?)null);

            EventPullResult result = await Sut().PullAccountEventsAsync(_accountId, null, null, CancellationToken.None);

            Assert.Equal(default, result);
        }

        [Fact]
        public async Task PullAccountEventsAsync_DiscoveryFails_ReturnsEmptyResult()
        {
            SetupAccount();
            _discovery.Setup(d => d.GetLocationsAsync(_accountId, It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("no network"));

            EventPullResult result = await Sut().PullAccountEventsAsync(_accountId, null, null, CancellationToken.None);

            Assert.Equal(default, result);
        }

        private (DeviceEntity Device, LocationEntity Location) WireOneDevice()
        {
            SetupAccount();
            var location = new LocationEntity { Id = Guid.NewGuid(), ProviderLocationId = "loc-1", Name = "Home" };
            DeviceEntity device = Device(location.Id, "dev-a");
            DiscoveryStub.Wire(_accountId, location, new[] { device }, _discovery, _locations, _devices);
            return (device, location);
        }

        [Fact]
        public async Task PullAccountEventsAsync_ToProvided_PassesEndDateToProvider()
        {
            WireOneDevice();
            var from = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
            var to = new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc);
            _events.Setup(s => s.GetEventsAsync(_accountId, "dev-a", from, to, null, It.IsAny<CancellationToken>())).ReturnsAsync(new List<DeviceEvent>());

            await Sut().PullAccountEventsAsync(_accountId, from, to, CancellationToken.None);

            _events.Verify(s => s.GetEventsAsync(_accountId, "dev-a", from, to, null, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task PullAccountEventsAsync_ToNull_UsesNowAsEnd()
        {
            WireOneDevice();
            DateTime? capturedEnd = null;
            _events.Setup(s => s.GetEventsAsync(_accountId, "dev-a", It.IsAny<DateTime>(), It.IsAny<DateTime>(), null, It.IsAny<CancellationToken>()))
                .Callback<Guid, string, DateTime, DateTime, string?, CancellationToken>((_, _, _, end, _, _) => capturedEnd = end)
                .ReturnsAsync(new List<DeviceEvent>());
            DateTime before = DateTime.UtcNow;

            await Sut().PullAccountEventsAsync(_accountId, null, null, CancellationToken.None);

            Assert.NotNull(capturedEnd);
            Assert.InRange(capturedEnd!.Value, before, DateTime.UtcNow);
        }

        [Fact]
        public async Task PullAccountEventsAsync_ProviderReturnsEventsOutsideWindow_CountsOnlyEventsInWindow()
        {
            WireOneDevice();
            var from = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
            var to = new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc);
            var inside = new DeviceEvent("in", "dev-a", "motion", new DateTime(2026, 9, 5, 0, 0, 0, DateTimeKind.Utc));
            var tooLate = new DeviceEvent("late", "dev-a", "motion", new DateTime(2026, 9, 11, 0, 0, 0, DateTimeKind.Utc));
            var tooEarly = new DeviceEvent("early", "dev-a", "motion", new DateTime(2026, 8, 30, 0, 0, 0, DateTimeKind.Utc));
            _events.Setup(s => s.GetEventsAsync(_accountId, "dev-a", It.IsAny<DateTime>(), It.IsAny<DateTime>(), null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<DeviceEvent> { inside, tooLate, tooEarly });

            EventPullResult result = await Sut().PullAccountEventsAsync(_accountId, from, to, CancellationToken.None);

            Assert.Equal(1, result.EventsSynced);
        }
    }
}
