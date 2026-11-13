using Microsoft.Extensions.Logging.Abstractions;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Core.Providers.Common.Contracts;

using ProviderAccountEntity = VideoForensics.Data.Common.Entities.ProviderAccount;

namespace VideoForensics.Client.Hosting.Tests.Services
{
    /// <summary>A Ring rate-limit ban stops event pulls before any provider call is made, and lifts when it expires.</summary>
    public class EventPullServiceRateLimitBanTests
    {
        private readonly Guid _accountId = Guid.NewGuid();
        private readonly Guid _locationId = Guid.NewGuid();
        private readonly Mock<IEventAndConfigService> _events = new();
        private readonly Mock<IDeviceWriteRepository> _devices = new();
        private readonly Mock<ILocationRepository> _locations = new();
        private readonly Mock<IProviderAccountRepository> _accounts = new();
        private readonly Mock<IDeviceDiscoveryService> _discovery = new();
        private readonly Mock<IMediaDownloadService> _media = new();

        private EventPullService BuildSut(IProviderApiBudgetGuard? budget = null)
        {
            var account = new ProviderAccountEntity { Id = _accountId, UserId = Guid.NewGuid(), ProviderName = "Ring", LinkedUtc = DateTime.UtcNow, IsActive = true };
            var location = new LocationEntity { Id = _locationId, ProviderLocationId = "location-123", Name = "Home" };
            var device = new DeviceEntity { Id = Guid.NewGuid(), LocationId = _locationId, ProviderDeviceId = "dev-1", Name = "Front Door", Type = "doorbell" };

            _ = _accounts.Setup(r => r.GetAsync(_accountId, It.IsAny<CancellationToken>())).ReturnsAsync(account);
            DiscoveryStub.Wire(_accountId, location, new[] { device }, _discovery, _locations, _devices);
            _ = _events.Setup(e => e.GetEventsAsync(_accountId, It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<DeviceEvent>());
            _ = _media.Setup(m => m.DownloadVideosAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DownloadResult(Success: true));

            return new EventPullService(_events.Object, _devices.Object, _locations.Object, _accounts.Object, NullLogger<EventPullService>.Instance, _media.Object, budgetGuard: budget);
        }

        [Fact]
        public async Task PullAccountEventsAsync_WhileRingBanIsActive_MakesNoEventCall()
        {
            _ = _media.Setup(m => m.GetRateLimitBanUntilUtc()).Returns(DateTime.UtcNow.AddMinutes(10));
            EventPullService sut = BuildSut();

            EventPullResult result = await sut.PullAccountEventsAsync(_accountId, null, null, CancellationToken.None);

            _events.Verify(e => e.GetEventsAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
            Assert.Equal(0, result.EventsSynced);
        }

        [Fact]
        public async Task PullAccountEventsAsync_ThrottledOnFirstDevice_StopsTheAccountPullAndFlagsIt()
        {
            var account = new ProviderAccountEntity { Id = _accountId, UserId = Guid.NewGuid(), ProviderName = "Ring", LinkedUtc = DateTime.UtcNow, IsActive = true };
            var location = new LocationEntity { Id = _locationId, ProviderLocationId = "location-123", Name = "Home" };
            var first = new DeviceEntity { Id = Guid.NewGuid(), LocationId = _locationId, ProviderDeviceId = "dev-1", Name = "Front Door", Type = "doorbell" };
            var second = new DeviceEntity { Id = Guid.NewGuid(), LocationId = _locationId, ProviderDeviceId = "dev-2", Name = "Flood", Type = "doorbell" };
            _ = _accounts.Setup(r => r.GetAsync(_accountId, It.IsAny<CancellationToken>())).ReturnsAsync(account);
            DiscoveryStub.Wire(_accountId, location, new[] { first, second }, _discovery, _locations, _devices);
            _ = _events.Setup(e => e.GetEventsAsync(_accountId, "dev-1", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new VideoForensics.Providers.Ring.Exceptions.ThrottledException("Ring has rate-limited this account.", isHardBan: false));
            _ = _events.Setup(e => e.GetEventsAsync(_accountId, "dev-2", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<DeviceEvent>());
            EventPullService sut = new(_events.Object, _devices.Object, _locations.Object, _accounts.Object, NullLogger<EventPullService>.Instance, _media.Object);

            EventPullResult result = await sut.PullAccountEventsAsync(_accountId, null, null, CancellationToken.None);

            _events.Verify(e => e.GetEventsAsync(_accountId, "dev-2", It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
            Assert.True(result.RateLimited);
        }

        [Fact]
        public async Task PullAccountEventsAsync_BudgetSpent_MakesNoEventCall()
        {
            var budget = new Mock<IProviderApiBudgetGuard>();
            _ = budget.Setup(b => b.TryConsumeAsync("Ring", It.IsAny<CancellationToken>())).ReturnsAsync(false);
            EventPullService sut = BuildSut(budget.Object);

            EventPullResult result = await sut.PullAccountEventsAsync(_accountId, null, null, CancellationToken.None);

            _events.Verify(e => e.GetEventsAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
            Assert.True(result.RateLimited);
        }

        [Fact]
        public async Task PullAccountEventsAsync_EachDeviceCall_IsCountedAgainstTheBudget()
        {
            var budget = new Mock<IProviderApiBudgetGuard>();
            _ = budget.Setup(b => b.TryConsumeAsync("Ring", It.IsAny<CancellationToken>())).ReturnsAsync(true);
            EventPullService sut = BuildSut(budget.Object);

            _ = await sut.PullAccountEventsAsync(_accountId, null, null, CancellationToken.None);

            // One device: the event fetch and the media download are each a Ring call.
            budget.Verify(b => b.RecordCallAsync("Ring", It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        [Fact]
        public async Task PullAccountEventsAsync_AfterRingBanExpires_PullsEvents()
        {
            _ = _media.Setup(m => m.GetRateLimitBanUntilUtc()).Returns(DateTime.UtcNow.AddMinutes(-1));
            EventPullService sut = BuildSut();

            _ = await sut.PullAccountEventsAsync(_accountId, null, null, CancellationToken.None);

            _events.Verify(e => e.GetEventsAsync(_accountId, It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        }
    }
}
