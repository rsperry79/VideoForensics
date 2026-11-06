using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Hosting.Contracts;
using VideoForensics.Client.Hosting.Services;
using VideoForensics.Core.Helpers.Platform;
using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

using ProviderAccountEntity = VideoForensics.Data.Common.Entities.ProviderAccount;

namespace VideoForensics.Client.Hosting.Tests.Services
{
    /// <summary>
    /// Drives the real AccountSyncRunner over the real EventPullService, with fakes only at the provider and repository edges.
    /// Proves the media windows and folders, that a sync never moves LastDownloadTimeUtc, that progress returns to idle, and
    /// that one failing device does not stop the other. Temp folders only.
    /// </summary>
    public class AccountSyncEndToEndTests : IDisposable
    {
        private static readonly DateTime End = new(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime KnownWatermark = new(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc);

        private readonly Guid _accountId = Guid.NewGuid();
        private readonly Guid _locationId = Guid.NewGuid();
        private readonly string _root = Path.Combine(Path.GetTempPath(), "vf-accountsync-e2e-" + Guid.NewGuid().ToString("N"));
        private readonly LocationEntity _location;
        private readonly DeviceEntity _frontDoor;
        private readonly DeviceEntity _backDoor;
        private readonly ProviderAccountEntity _account;
        private readonly Mock<IEventAndConfigService> _events = new();
        private readonly Mock<IDeviceRepository> _deviceRepository = new();
        private readonly Mock<ILocationRepository> _locationRepository = new();
        private readonly Mock<IProviderAccountRepository> _accounts = new();
        private readonly Mock<IDeviceDiscoveryService> _discovery = new();
        private readonly Mock<IMediaDownloadService> _media = new();
        private readonly Mock<IProviderAuthService> _auth = new();
        private readonly Mock<IProviderSyncRunRepository> _runs = new();
        private readonly Mock<ISnapshotSyncService> _snapshots = new();
        private readonly List<(string DeviceId, string Folder, DateTime Start, DateTime End)> _downloads = [];
        private readonly List<DateTime?> _watermarksWritten = [];

        public AccountSyncEndToEndTests()
        {
            _location = new LocationEntity { Id = _locationId, ProviderLocationId = "loc-1", Name = "Home" };
            _frontDoor = new DeviceEntity { Id = Guid.NewGuid(), LocationId = _locationId, ProviderDeviceId = "dev-1", Name = "Front Door", Type = "doorbell" };
            _backDoor = new DeviceEntity { Id = Guid.NewGuid(), LocationId = _locationId, ProviderDeviceId = "dev-2", Name = "Back Door", Type = "doorbell" };

            _account = new ProviderAccountEntity
            {
                Id = _accountId,
                UserId = Guid.NewGuid(),
                ProviderName = "Ring",
                LinkedUtc = DateTime.UtcNow,
                IsActive = true,
                LastDownloadTimeUtc = KnownWatermark
            };

            _ = _accounts.Setup(r => r.GetAsync(_accountId, It.IsAny<CancellationToken>())).ReturnsAsync(_account);
            _ = _accounts.Setup(r => r.UpdateAsync(It.IsAny<ProviderAccountEntity>(), It.IsAny<CancellationToken>()))
                .Callback<ProviderAccountEntity, CancellationToken>((account, _) => _watermarksWritten.Add(account.LastDownloadTimeUtc));

            DiscoveryStub.Wire(_accountId, _location, new[] { _frontDoor, _backDoor }, _discovery, _locationRepository, _deviceRepository);

            _ = _auth.Setup(a => a.RestoreFromSavedCredentialsAsync(_accountId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _ = _runs.Setup(r => r.AddAsync(It.IsAny<ProviderSyncRun>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            _ = _events.Setup(s => s.GetEventsAsync(_accountId, It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<DeviceEvent>());

            _ = _media.Setup(m => m.DownloadVideosAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .Returns<string, string, DateTime, DateTime, string?, CancellationToken>((deviceId, folder, start, end, _, _) =>
                {
                    _downloads.Add((deviceId, folder, start, end));
                    return Task.FromResult(new DownloadResult(Success: true, FilesDownloaded: 2));
                });
            _ = _media.Setup(m => m.GetStatus()).Returns(new DownloadStatus(false, 0, 0, 0L, null));
        }

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }

        /// <summary>The real runner over the real pull service; only the provider and repository edges are fakes.</summary>
        private AccountSyncRunner BuildRunner(ISyncProgressStore? progress = null)
        {
            var eventPull = new EventPullService(
                _events.Object,
                _deviceRepository.Object,
                _locationRepository.Object,
                _accounts.Object,
                mediaDownloadService: _media.Object,
                mediaRoot: _root,
                progress: progress,
                progressPollInterval: TimeSpan.FromMilliseconds(20));

            var services = new ServiceCollection().AddScoped(_ => Mock.Of<IInventorySyncService>())
                .AddScoped(_ => _auth.Object)
                .AddScoped<IEventPullService>(_ => eventPull)
                .BuildServiceProvider();

            return new AccountSyncRunner(
                services,
                _accounts.Object,
                _runs.Object,
                _snapshots.Object,
                Mock.Of<ILogger<AccountSyncRunner>>(),
                new SyncActivityLog());
        }

        [Fact]
        public async Task AccountSyncRunner_CustomTwentyDayEventsSync_DownloadsEveryDeviceInEachWindowOldestFirst()
        {
            DateTime from = End.AddDays(-20);

            await BuildRunner().RunAsync(_accountId, new AccountSyncRequest(AccountSyncType.Events, from, End, AllHistory: false), CancellationToken.None);

            var expected = new List<(string DeviceId, string Folder, DateTime Start, DateTime End)>();
            foreach ((DateTime start, DateTime end) in AccountSyncRunner.WeeklyWindows(from, End))
            {
                expected.Add(("dev-1", MediaFolderLayout.GetDeviceFolder(_root, "Ring", "Home", "Front Door"), start, end));
                expected.Add(("dev-2", MediaFolderLayout.GetDeviceFolder(_root, "Ring", "Home", "Back Door"), start, end));
            }

            Assert.Equal(6, _downloads.Count);
            Assert.Equal(expected, _downloads);
            Assert.All(_downloads, d => Assert.StartsWith(Path.GetFullPath(_root), Path.GetFullPath(d.Folder)));
        }

        [Fact]
        public async Task AccountSyncRunner_CustomTwentyDayEventsSync_NeverMovesLastDownloadTimeUtc()
        {
            DateTime from = End.AddDays(-20);

            await BuildRunner().RunAsync(_accountId, new AccountSyncRequest(AccountSyncType.Events, from, End, AllHistory: false), CancellationToken.None);

            // The sync did finish (it wrote its display timestamp), so the watermark check is not vacuous.
            Assert.NotNull(_account.LastSyncCompletedUtc);
            Assert.NotEmpty(_watermarksWritten);
            Assert.All(_watermarksWritten, watermark => Assert.Equal(KnownWatermark, watermark));
            Assert.Equal(KnownWatermark, _account.LastDownloadTimeUtc);
        }

        [Fact]
        public async Task AccountSyncRunner_SyncFinishes_LeavesSyncProgressIdle()
        {
            var store = new SyncProgressStore();
            DateTime from = End.AddDays(-20);

            await BuildRunner(store).RunAsync(_accountId, new AccountSyncRequest(AccountSyncType.Events, from, End, AllHistory: false), CancellationToken.None);

            Assert.Equal(SyncProgress.Idle, store.Current);
            Assert.Equal(6, _downloads.Count);
        }

        [Fact]
        public async Task AccountSyncRunner_OneDeviceMediaFails_CompletesOtherDeviceAndRecordsFailedFeed()
        {
            _ = _media.Setup(m => m.DownloadVideosAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .Returns<string, string, DateTime, DateTime, string?, CancellationToken>((deviceId, _, _, _, _, _) => deviceId == "dev-2"
                    ? Task.FromException<DownloadResult>(new InvalidOperationException("Provider rejected media request"))
                    : Task.FromResult(new DownloadResult(Success: true, FilesDownloaded: 2)));
            DateTime from = End.AddDays(-3);

            await BuildRunner().RunAsync(_accountId, new AccountSyncRequest(AccountSyncType.Events, from, End, AllHistory: false), CancellationToken.None);

            _media.Verify(m => m.DownloadVideosAsync("dev-1", It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
            _media.Verify(m => m.DownloadVideosAsync("dev-2", It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
            _runs.Verify(r => r.AddAsync(
                It.Is<ProviderSyncRun>(run => !run.Success
                    && run.FeedsFailed == 1
                    && run.DevicesSynced == 2
                    && run.ErrorMessage != null
                    && run.ErrorMessage.Contains("Back Door")),
                It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
