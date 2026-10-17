namespace VideoForensics.Hosting.Tests.Services
{
    using System.Collections.Concurrent;
    using VideoForensics.Core.Providers.Common.Contracts;
    using LocationEntity = VideoForensics.Data.Common.Entities.Location;
    using DeviceEntity = VideoForensics.Data.Common.Entities.Device;
    using ProviderAccountEntity = VideoForensics.Data.Common.Entities.ProviderAccount;

    /// <summary>
    /// Two or more accounts pulled at once must never see each other's devices or write media into each other's folders.
    /// Media is per account: the pull writes under the account's own folder and only for its own devices.
    /// </summary>
    public class EventPullServiceIntegrationTests
    {
        private const string MediaRoot = @"C:\media-root";

        private sealed class Account
        {
            public Guid Id { get; init; }
            public string DeviceId { get; init; } = "";
            public LocationEntity Location { get; init; } = null!;
            public DeviceEntity Device { get; init; } = null!;
            public ProviderAccountEntity Entity { get; init; } = null!;
        }

        private static Account MakeAccount(string deviceId)
        {
            var id = Guid.NewGuid();
            var location = new LocationEntity { Id = Guid.NewGuid(), ProviderLocationId = "location-" + deviceId, Name = "Home" };
            // Device and account rows need every required member; the pull reads only their ids.
            return new Account
            {
                Id = id,
                DeviceId = deviceId,
                Location = location,
                Device = new DeviceEntity { Id = Guid.NewGuid(), LocationId = location.Id, ProviderDeviceId = deviceId, Name = deviceId, Type = "doorbell" },
                Entity = new ProviderAccountEntity { Id = id, UserId = Guid.NewGuid(), ProviderName = "Ring", LinkedUtc = DateTime.UtcNow, IsActive = true },
            };
        }

        private static Mock<IMediaDownloadService> RecordingMedia(ConcurrentBag<(string Device, string Folder)> calls)
        {
            var media = new Mock<IMediaDownloadService>();
            media.Setup(m => m.DownloadVideosAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .Callback<string, string, DateTime, DateTime, string?, CancellationToken>((device, folder, _, _, _, _) => calls.Add((device, folder)))
                .ReturnsAsync(new DownloadResult(Success: true));
            return media;
        }

        private static (EventPullService Sut, Mock<IEventAndConfigService> Events, Mock<IProviderAccountRepository> Accounts)
            Build(IEnumerable<Account> accounts, Mock<IMediaDownloadService> media)
        {
            var events = new Mock<IEventAndConfigService>();
            var devices = new Mock<IDeviceRepository>();
            var locations = new Mock<ILocationRepository>();
            var accountRepo = new Mock<IProviderAccountRepository>();
            var discovery = new Mock<IDeviceDiscoveryService>();

            foreach (Account a in accounts)
            {
                accountRepo.Setup(r => r.GetAsync(a.Id, It.IsAny<CancellationToken>())).ReturnsAsync(a.Entity);
                DiscoveryStub.Wire(a.Id, a.Location, new[] { a.Device }, discovery, locations, devices);
            }

            var sut = new EventPullService(
                events.Object, devices.Object, locations.Object, accountRepo.Object, discovery.Object,
                mediaDownloadService: media.Object, mediaRoot: MediaRoot, minRequestInterval: TimeSpan.Zero);
            return (sut, events, accountRepo);
        }

        [Fact]
        public async Task PullAccountEventsAsync_TwoAccountsConcurrent_NoDataCrossContamination()
        {
            Account first = MakeAccount("device-account1");
            Account second = MakeAccount("device-account2");
            var calls = new ConcurrentBag<(string Device, string Folder)>();
            var (sut, events, _) = Build(new[] { first, second }, RecordingMedia(calls));

            await Task.WhenAll(
                sut.PullAccountEventsAsync(first.Id, fromTimestampUtc: DateTime.UtcNow.AddDays(-5), CancellationToken.None),
                sut.PullAccountEventsAsync(second.Id, fromTimestampUtc: DateTime.UtcNow.AddDays(-5), CancellationToken.None));

            // Each device was pulled, into its own account's folder, and never into the other's.
            Assert.Contains(calls, c => c.Device == "device-account1" && c.Folder.EndsWith(first.Id.ToString("N")));
            Assert.Contains(calls, c => c.Device == "device-account2" && c.Folder.EndsWith(second.Id.ToString("N")));
            Assert.DoesNotContain(calls, c => c.Device == "device-account1" && c.Folder.EndsWith(second.Id.ToString("N")));
            Assert.DoesNotContain(calls, c => c.Device == "device-account2" && c.Folder.EndsWith(first.Id.ToString("N")));
        }

        [Fact]
        public async Task PullAccountEventsAsync_DifferentAccounts_ReceiveDifferentDevices()
        {
            Account first = MakeAccount("dev1");
            Account second = MakeAccount("dev2");
            var calls = new ConcurrentBag<(string Device, string Folder)>();
            var (sut, _, _) = Build(new[] { first, second }, RecordingMedia(calls));

            await sut.PullAccountEventsAsync(first.Id, fromTimestampUtc: DateTime.UtcNow.AddDays(-5), CancellationToken.None);

            // Only the first account's device was pulled.
            Assert.All(calls, c => Assert.Equal("dev1", c.Device));
            Assert.NotEmpty(calls);
        }

        [Fact]
        public async Task PullAccountEventsAsync_SingleAccount_BackwardCompatible()
        {
            Account only = MakeAccount("device-123");
            var calls = new ConcurrentBag<(string Device, string Folder)>();
            var (sut, _, accountRepo) = Build(new[] { only }, RecordingMedia(calls));

            await sut.PullAccountEventsAsync(only.Id, fromTimestampUtc: DateTime.UtcNow.AddDays(-5), CancellationToken.None);

            accountRepo.Verify(r => r.GetAsync(only.Id, It.IsAny<CancellationToken>()), Times.Once);
            Assert.Single(calls);
            Assert.Equal("device-123", calls.Single().Device);
        }
    }
}
