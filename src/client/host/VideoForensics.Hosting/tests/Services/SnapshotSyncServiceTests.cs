using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Core.Helpers.Platform;
using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Client.Hosting.Contracts;
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
            return Sut(new ServiceCollection().AddScoped(_ => _download.Object));
        }

        private ISnapshotSyncService Sut(IServiceCollection services)
        {
            return new SnapshotSyncService(
            services.BuildServiceProvider(), _accounts.Object, _locations.Object, _devices.Object, _storage.Object, Mock.Of<ILogger<SnapshotSyncService>>());
        }

        [Fact]
        public async Task SyncAccountSnapshotsAsync_ScheduleHasRollupInterval_SetsWarningWindowToIt()
        {
            AddDevices("1");
            DownloadReturns("1", new DownloadResult(true, FilesDownloaded: 1));
            var schedules = new Mock<VideoForensics.Data.Common.Contracts.ISyncScheduleRepository>();
            _ = schedules.Setup(s => s.GetByProviderAccountIdAsync(_accountId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new VideoForensics.Data.Common.Entities.SyncSchedule
                {
                    Id = Guid.NewGuid(),
                    ProviderAccountId = _accountId,
                    SnapshotRollupIntervalMinutes = 30,
                });
            IServiceCollection services = new ServiceCollection().AddScoped(_ => _download.Object).AddScoped(_ => schedules.Object);

            _ = await Sut(services).SyncAccountSnapshotsAsync(_accountId, null, null, CancellationToken.None);

            _download.Verify(d => d.SetSnapshotWarningWindow(TimeSpan.FromMinutes(30)), Times.Once);
        }

        [Fact]
        public async Task SyncAccountSnapshotsAsync_NoScheduleRepository_SetsWarningWindowToDefault()
        {
            AddDevices("1");
            DownloadReturns("1", new DownloadResult(true, FilesDownloaded: 1));

            _ = await Sut().SyncAccountSnapshotsAsync(_accountId, null, null, CancellationToken.None);

            _download.Verify(d => d.SetSnapshotWarningWindow(TimeSpan.FromMinutes(VideoForensics.Data.Common.Entities.SyncSchedule.DefaultSnapshotRollupIntervalMinutes)), Times.Once);
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
                .ReturnsAsync(new List<DeviceEntity>
                {
                    new() { Id = Guid.NewGuid(), LocationId = _location.Id, ProviderDeviceId = "1", Name = "Flood", Type = "doorbot", SnapshotCaptureEnabled = false },
                    new() { Id = Guid.NewGuid(), LocationId = _location.Id, ProviderDeviceId = "2", Name = "Front", Type = "doorbot", SnapshotCaptureEnabled = true },
                });
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
    }
}
