using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Hosting.Contracts;
using VideoForensics.Core.Helpers.Platform;
using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Providers.Ring.Services;

using ProviderAccountEntity = VideoForensics.Data.Common.Entities.ProviderAccount;

namespace VideoForensics.Client.Hosting.Tests.Services
{
    /// <summary>Snapshot sync fetches snapshots for every device of an account through the provider-neutral download contract.</summary>
    public class SnapshotSyncServiceTests
    {
        private const string MediaRoot = "C:\\media-root";

        private readonly Mock<IMediaDownloadService> _download = new();
        private readonly Mock<IProviderAccountRepository> _accounts = new();
        private readonly Mock<ILocationRepository> _locations = new();
        private readonly Mock<IDeviceRepository> _devices = new();
        private readonly Mock<IStorageSettingsService> _storage = new();
        private readonly Guid _accountId = Guid.NewGuid();
        private readonly LocationEntity _location = new() { Id = Guid.NewGuid(), ProviderLocationId = "loc-1", Name = "Home" };

        public SnapshotSyncServiceTests()
        {
            _ = _accounts.Setup(r => r.GetAsync(_accountId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProviderAccountEntity { Id = _accountId, UserId = Guid.NewGuid(), ProviderName = "Ring", LinkedUtc = DateTime.UtcNow, IsActive = true });
            _ = _locations.Setup(r => r.ListByProviderAccountIdAsync(_accountId, It.IsAny<CancellationToken>())).ReturnsAsync(new[] { _location });
            _ = _storage.Setup(s => s.GetStatusAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new StorageSettings(new[] { new StorageCategoryStatus(StorageCategory.Media, MediaRoot, true, 0, 0, true, false) }));
        }

        private ISnapshotSyncService Sut()
        {
            return Sut(new ServiceCollection()
                .AddScoped(_ => _download.Object)
                .AddScoped(_ => ConfigService(15).Object));
        }

        private ISnapshotSyncService Sut(IServiceCollection services)
        {
            // Every host registers the configuration service; a test that does not care about it gets the default one.
            if (!services.Any(d => d.ServiceType == typeof(IForensicsConfigurationService)))
            {
                _ = services.AddScoped(_ => ConfigService(15).Object);
            }

            return new SnapshotSyncService(
            services.BuildServiceProvider(), _accounts.Object, _locations.Object, _devices.Object, _storage.Object, Mock.Of<ILogger<SnapshotSyncService>>());
        }

        [Fact]
        public async Task SyncAccountSnapshotsAsync_NoConfigurationService_Throws()
        {
            // The check interval is required: without it the warning window cannot be set, so the sync must fail, not guess.
            AddDevices("1");
            DownloadReturns("1", new DownloadResult(true, FilesDownloaded: 1));
            var services = new ServiceCollection().AddScoped(_ => _download.Object);
            ISnapshotSyncService sut = new SnapshotSyncService(
                services.BuildServiceProvider(), _accounts.Object, _locations.Object, _devices.Object, _storage.Object, Mock.Of<ILogger<SnapshotSyncService>>());

            _ = await Assert.ThrowsAnyAsync<InvalidOperationException>(() => sut.SyncAccountSnapshotsAsync(_accountId, null, null, CancellationToken.None));
        }

        [Fact]
        public async Task SyncAccountSnapshotsAsync_NoScheduleRepository_SetsWarningWindowToDefault()
        {
            AddDevices("1");
            DownloadReturns("1", new DownloadResult(true, FilesDownloaded: 1));

            _ = await Sut().SyncAccountSnapshotsAsync(_accountId, null, null, CancellationToken.None);

            _download.Verify(d => d.SetSnapshotWarningWindow(TimeSpan.FromMinutes(15)), Times.Once);
        }

        [Fact]
        public async Task SyncAccountSnapshotsAsync_WarningWindowFollowsServicesCheckInterval()
        {
            AddDevices("1");
            DownloadReturns("1", new DownloadResult(true, FilesDownloaded: 1));
            var services = new ServiceCollection()
                .AddScoped(_ => _download.Object)
                .AddScoped(_ => ConfigService(30).Object);

            _ = await Sut(services).SyncAccountSnapshotsAsync(_accountId, null, null, CancellationToken.None);

            _download.Verify(d => d.SetSnapshotWarningWindow(TimeSpan.FromMinutes(30)), Times.Once);
        }

        private static Mock<IForensicsConfigurationService> ConfigService(int checkIntervalMinutes)
        {
            var config = new ForensicsConfiguration { TimelapseCheckIntervalMinutes = checkIntervalMinutes };
            var mock = new Mock<IForensicsConfigurationService>();
            _ = mock.Setup(s => s.LoadConfigurationAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(config);
            return mock;
        }

        private void AddDevices(params string[] providerIds)
        {
            _ = _devices
            .Setup(r => r.GetByLocationIdAsync(_location.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(providerIds.Select(id => new DeviceEntity { Id = Guid.NewGuid(), LocationId = _location.Id, ProviderDeviceId = id, Name = $"Cam {id}", Type = "doorbot" }).ToList());
        }

        private void DownloadReturns(string deviceId, DownloadResult result)
        {
            _ = _download
            .Setup(d => d.DownloadSnapshotsAsync(deviceId, It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
        }

        [Fact]
        public async Task SyncAccountSnapshotsAsync_TwoDevices_DownloadsEachAndSumsFiles()
        {
            AddDevices("1", "2");
            DownloadReturns("1", new DownloadResult(true, FilesDownloaded: 1));
            DownloadReturns("2", new DownloadResult(true, FilesDownloaded: 1));

            SnapshotSyncResult result = await Sut().SyncAccountSnapshotsAsync(_accountId, null, null, CancellationToken.None);

            Assert.Equal(new SnapshotSyncResult(DevicesAttempted: 2, DevicesSucceeded: 2, SnapshotsSaved: 2, DevicesFailed: 0, ErrorMessage: null), result);
        }

        [Fact]
        public async Task SyncAccountSnapshotsAsync_DeviceDownloadFails_CountsFailureAndContinues()
        {
            AddDevices("1", "2");
            DownloadReturns("1", new DownloadResult(false, ErrorMessage: "No snapshot available for this device"));
            DownloadReturns("2", new DownloadResult(true, FilesDownloaded: 1));

            SnapshotSyncResult result = await Sut().SyncAccountSnapshotsAsync(_accountId, null, null, CancellationToken.None);

            Assert.Equal(2, result.DevicesAttempted);
            Assert.Equal(1, result.DevicesSucceeded);
            Assert.Equal(1, result.SnapshotsSaved);
            Assert.Equal(1, result.DevicesFailed);
            Assert.Contains("No snapshot available", result.ErrorMessage);
        }

        [Fact]
        public async Task SyncAccountSnapshotsAsync_DownloadThrows_CountsFailureAndContinues()
        {
            AddDevices("1", "2");
            _ = _download.Setup(d => d.DownloadSnapshotsAsync("1", It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("boom"));
            DownloadReturns("2", new DownloadResult(true, FilesDownloaded: 1));

            SnapshotSyncResult result = await Sut().SyncAccountSnapshotsAsync(_accountId, null, null, CancellationToken.None);

            Assert.Equal(1, result.DevicesFailed);
            Assert.Equal(1, result.DevicesSucceeded);
            Assert.Contains("boom", result.ErrorMessage);
        }

        [Fact]
        public async Task SyncAccountSnapshotsAsync_NoDevices_ReturnsZeroWithoutDownloading()
        {
            AddDevices();

            SnapshotSyncResult result = await Sut().SyncAccountSnapshotsAsync(_accountId, null, null, CancellationToken.None);

            Assert.Equal(default, result);
            _download.Verify(d => d.DownloadSnapshotsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task SyncAccountSnapshotsAsync_Window_PassesWindowLocationAndMediaPathToProvider()
        {
            AddDevices("1");
            DownloadReturns("1", new DownloadResult(true, FilesDownloaded: 1));
            var from = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
            var to = new DateTime(2026, 9, 10, 0, 0, 0, DateTimeKind.Utc);

            _ = await Sut().SyncAccountSnapshotsAsync(_accountId, from, to, CancellationToken.None);

            _download.Verify(d => d.DownloadSnapshotsAsync(
                "1",
                Path.Combine(MediaRoot, "ring", "home", "cam_1"),
                from,
                to,
                "loc-1",
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SyncAccountSnapshotsAsync_Always_AttributesDownloadsToTheAccount()
        {
            AddDevices("1");
            DownloadReturns("1", new DownloadResult(true, FilesDownloaded: 1));

            _ = await Sut().SyncAccountSnapshotsAsync(_accountId, null, null, CancellationToken.None);

            _download.Verify(d => d.SetActiveProviderAccountId(_accountId), Times.Once);
        }

        [Fact]
        public async Task SyncAccountSnapshotsAsync_NoMediaStorageConfigured_ReturnsErrorWithoutDownloading()
        {
            AddDevices("1");
            _ = _storage.Setup(s => s.GetStatusAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new StorageSettings(Array.Empty<StorageCategoryStatus>()));

            SnapshotSyncResult result = await Sut().SyncAccountSnapshotsAsync(_accountId, null, null, CancellationToken.None);

            Assert.NotNull(result.ErrorMessage);
            _download.Verify(d => d.DownloadSnapshotsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task SyncAccountSnapshotsAsync_AccountMissing_ReturnsError()
        {
            _ = _accounts.Setup(r => r.GetAsync(_accountId, It.IsAny<CancellationToken>())).ReturnsAsync((ProviderAccountEntity?)null);

            SnapshotSyncResult result = await Sut().SyncAccountSnapshotsAsync(_accountId, null, null, CancellationToken.None);

            Assert.Equal("Account not found", result.ErrorMessage);
        }

        [Fact]
        public async Task SyncAccountSnapshotsAsync_TwoProvidersRegistered_UsesTheProviderOwningTheAccount()
        {
            var ring = new Mock<IMediaDownloadService>();
            var wyze = new Mock<IMediaDownloadService>();
            foreach (Mock<IMediaDownloadService>? m in new[] { ring, wyze })
            {
                _ = m.Setup(d => d.DownloadSnapshotsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new DownloadResult(true, FilesDownloaded: 1));
            }

            // Mirrors AddVideoForensicsMultiProviderServices: keyed per provider, with the unkeyed default being whichever was registered last.
            var services = new ServiceCollection();
            _ = services.AddKeyedScoped<IMediaDownloadService>("Ring", (_, _) => ring.Object);
            _ = services.AddKeyedScoped<IMediaDownloadService>("Wyze", (_, _) => wyze.Object);
            _ = services.AddScoped(_ => ring.Object);
            _ = _accounts.Setup(r => r.GetAsync(_accountId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProviderAccountEntity { Id = _accountId, UserId = Guid.NewGuid(), ProviderName = "Wyze", LinkedUtc = DateTime.UtcNow, IsActive = true });
            AddDevices("1");

            _ = await Sut(services).SyncAccountSnapshotsAsync(_accountId, null, null, CancellationToken.None);

            wyze.Verify(d => d.SetActiveProviderAccountId(_accountId), Times.Once);
            wyze.Verify(d => d.DownloadSnapshotsAsync("1", It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
            ring.Verify(d => d.DownloadSnapshotsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        private void AddDevicesWithState(params (string Id, bool? IsOnline)[] devices)
        {
            _ = _devices
            .Setup(r => r.GetByLocationIdAsync(_location.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(devices.Select(d => new DeviceEntity { Id = Guid.NewGuid(), LocationId = _location.Id, ProviderDeviceId = d.Id, Name = $"Cam {d.Id}", Type = "doorbot", IsOnline = d.IsOnline }).ToList());
        }

        [Fact]
        public async Task SyncAccountSnapshotsAsync_RingOfflineDevice_IsSkippedAndNotCounted()
        {
            AddDevicesWithState(("1", false), ("2", true));
            DownloadReturns("2", new DownloadResult(true, FilesDownloaded: 1));
            IServiceCollection services = new ServiceCollection().AddScoped(_ => _download.Object);
            _ = services.AddKeyedSingleton<IProviderDeviceStatus>("Ring", new RingDeviceOnlineStatus());

            SnapshotSyncResult result = await Sut(services).SyncAccountSnapshotsAsync(_accountId, null, null, CancellationToken.None);

            _download.Verify(d => d.DownloadSnapshotsAsync("1", It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
            Assert.Equal(1, result.DevicesAttempted);
            Assert.Equal(1, result.DevicesSucceeded);
        }

        [Fact]
        public async Task SyncAccountSnapshotsAsync_DeviceWithSnapshotCaptureOff_IsSkippedAndNotCounted()
        {
            // The per-device switch is the user's "sync off" choice; the scheduled sync must honour it as capture does.
            _ = _devices
                .Setup(r => r.GetByLocationIdAsync(_location.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(
                [
                    new() { Id = Guid.NewGuid(), LocationId = _location.Id, ProviderDeviceId = "1", Name = "Flood", Type = "doorbot", SnapshotCaptureEnabled = false },
                    new() { Id = Guid.NewGuid(), LocationId = _location.Id, ProviderDeviceId = "2", Name = "Front", Type = "doorbot", SnapshotCaptureEnabled = true },
                ]);
            DownloadReturns("2", new DownloadResult(true, FilesDownloaded: 1));

            SnapshotSyncResult result = await Sut().SyncAccountSnapshotsAsync(_accountId, null, null, CancellationToken.None);

            _download.Verify(d => d.DownloadSnapshotsAsync("1", It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
            Assert.Equal(1, result.DevicesAttempted);
            Assert.Equal(0, result.DevicesFailed);
        }

        [Fact]
        public async Task SyncAccountSnapshotsAsync_ProviderWithoutStatusRule_KeepsOfflineDevice()
        {
            // Without a registered rule the sync has no basis to call a device offline, so it keeps the device.
            _ = _accounts.Setup(r => r.GetAsync(_accountId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProviderAccountEntity { Id = _accountId, UserId = Guid.NewGuid(), ProviderName = "Wyze", LinkedUtc = DateTime.UtcNow, IsActive = true });
            AddDevicesWithState(("1", false));
            DownloadReturns("1", new DownloadResult(true, FilesDownloaded: 1));

            SnapshotSyncResult result = await Sut(new ServiceCollection().AddScoped(_ => _download.Object))
                .SyncAccountSnapshotsAsync(_accountId, null, null, CancellationToken.None);

            _download.Verify(d => d.DownloadSnapshotsAsync("1", It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
            Assert.Equal(1, result.DevicesSucceeded);
        }

        [Fact]
        public async Task SyncAccountSnapshotsAsync_NoKeyedServiceForProvider_FallsBackToUnkeyedService()
        {
            DownloadReturns("1", new DownloadResult(true, FilesDownloaded: 1));
            AddDevices("1");

            SnapshotSyncResult result = await Sut(new ServiceCollection().AddScoped(_ => _download.Object))
                .SyncAccountSnapshotsAsync(_accountId, null, null, CancellationToken.None);

            Assert.Equal(1, result.DevicesSucceeded);
        }

        [Fact]
        public async Task RunCycleAsync_EnabledDeviceWithNoRecentAttempt_CapturesOnce()
        {
            // Same fixture as SnapshotCycle_EnabledDeviceWithNoRecentAttempt_CapturesOnce, driven through the sync service.
            var capture = new Mock<IProviderSnapshotCapture>();
            _ = capture.Setup(c => c.CaptureAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SnapshotCaptureAttempt(200, [0xFF, 0xD8, 0xFF, 0xE0], null));
            _ = _accounts.Setup(r => r.ListActiveAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { new ProviderAccountEntity { Id = _accountId, UserId = Guid.NewGuid(), ProviderName = "Ring", LinkedUtc = DateTime.UtcNow, IsActive = true } });
            _ = _devices.Setup(r => r.GetByLocationIdAsync(_location.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new[] { new DeviceEntity { Id = Guid.NewGuid(), LocationId = _location.Id, ProviderDeviceId = "12345", Name = "Front", Type = "doorbot", SnapshotCaptureEnabled = true } });

            string mediaRoot = Path.Combine(Path.GetTempPath(), "vf-sync-cycle-" + Guid.NewGuid().ToString("N"));
            try
            {
                var storage = new Mock<IStorageLocationProvider>();
                _ = storage.Setup(s => s.GetDefaultRoot(StorageCategory.Media)).Returns(mediaRoot);
                _ = storage.Setup(s => s.GetEffectiveRoot(StorageCategory.Media, It.IsAny<string?>())).Returns(mediaRoot);
                var captureRows = new Mock<ISnapshotCaptureRepository>();
                _ = captureRows.Setup(r => r.GetDeviceIdsWithAttemptsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((IReadOnlySet<Guid>)new HashSet<Guid>());
                var auth = new Mock<IProviderAuthService>();
                _ = auth.Setup(a => a.RestoreFromSavedCredentialsAsync(_accountId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
                var sessions = new Mock<ISessionProvider>();

                var services = new ServiceCollection();
                _ = services.AddSingleton(_accounts.Object);
                _ = services.AddSingleton(_locations.Object);
                _ = services.AddSingleton(_devices.Object);
                _ = services.AddSingleton(captureRows.Object);
                _ = services.AddSingleton(Mock.Of<ISyncScheduleRepository>());
                _ = services.AddSingleton(Mock.Of<IMediaItemRepository>());
                _ = services.AddSingleton(sessions.Object);
                _ = services.AddKeyedSingleton<IProviderSnapshotCapture>("Ring", capture.Object);
                _ = services.AddKeyedSingleton<IProviderAuthService>("Ring", auth.Object);
                _ = services.AddKeyedSingleton<IProviderDeviceStatus>("Ring", new RingDeviceOnlineStatus());
                _ = services.AddSingleton(new SnapshotCycle(Mock.Of<ILogger<SnapshotCycle>>(), TimeProvider.System, storage.Object, new ForensicsConfiguration()));

                using ServiceProvider provider = services.BuildServiceProvider();
                ISnapshotSyncService sut = new SnapshotSyncService(
                    provider, _accounts.Object, _locations.Object, _devices.Object, _storage.Object, Mock.Of<ILogger<SnapshotSyncService>>());

                await sut.RunCycleAsync(CancellationToken.None);
            }
            finally
            {
                if (Directory.Exists(mediaRoot))
                {
                    Directory.Delete(mediaRoot, recursive: true);
                }
            }

            capture.Verify(c => c.CaptureAsync(It.IsAny<Guid>(), "12345", It.IsAny<Guid>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
