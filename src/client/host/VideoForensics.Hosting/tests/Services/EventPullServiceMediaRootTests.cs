using VideoForensics.Client.Common.Contracts;
using VideoForensics.Core.Helpers.Platform;
using VideoForensics.Core.Providers.Common.Contracts;

using ProviderAccountEntity = VideoForensics.Data.Common.Entities.ProviderAccount;

namespace VideoForensics.Client.Hosting.Tests.Services
{
    /// <summary>
    /// Proves the event pull writes media under the Media root the user set on the Storage Settings page
    /// (IForensicsConfiguration.DownloadLocation), not the default root. Temp folders only.
    /// </summary>
    public class EventPullServiceMediaRootTests : IDisposable
    {
        private readonly Guid _accountId = Guid.NewGuid();
        private readonly Guid _locationId = Guid.NewGuid();
        private readonly string _root = Path.Combine(Path.GetTempPath(), "vf-eventpull-root-" + Guid.NewGuid().ToString("N"));
        private readonly LocationEntity _location;
        private readonly DeviceEntity _device;
        private readonly Mock<IEventAndConfigService> _events = new();
        private readonly Mock<IDeviceRepository> _deviceRepository = new();
        private readonly Mock<ILocationRepository> _locationRepository = new();
        private readonly Mock<IProviderAccountRepository> _accountRepository = new();
        private readonly Mock<IDeviceDiscoveryService> _discovery = new();
        private readonly Mock<IMediaDownloadService> _media = new();
        private readonly Mock<IStorageLocationProvider> _storage = new();
        private readonly Mock<IForensicsConfigurationService> _configurationService = new();
        private readonly Mock<IForensicsConfiguration> _config = new();

        public EventPullServiceMediaRootTests()
        {
            _location = new LocationEntity { Id = _locationId, ProviderLocationId = "loc-1", Name = "Home" };
            _device = new DeviceEntity { Id = Guid.NewGuid(), LocationId = _locationId, ProviderDeviceId = "dev-1", Name = "Front Door", Type = "doorbell" };

            _ = _accountRepository.Setup(r => r.GetAsync(_accountId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ProviderAccountEntity { Id = _accountId, ProviderName = "Ring", IsActive = true });
            DiscoveryStub.Wire(_accountId, _location, new[] { _device }, _discovery, _locationRepository, _deviceRepository);

            _ = _events.Setup(s => s.GetEventsAsync(_accountId, "dev-1", It.IsAny<DateTime>(), It.IsAny<DateTime>(), null, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<DeviceEvent>());
            _ = _media.Setup(m => m.DownloadVideosAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new DownloadResult(Success: true));

            // Mirrors the real provider: a blank override means the default root.
            _ = _storage.Setup(s => s.GetDefaultRoot(StorageCategory.Media)).Returns(DefaultRoot);
            _ = _storage.Setup(s => s.GetEffectiveRoot(StorageCategory.Media, It.IsAny<string?>()))
                .Returns<StorageCategory, string?>((_, configuredOverride) => string.IsNullOrWhiteSpace(configuredOverride) ? DefaultRoot : configuredOverride);

            _ = _configurationService.Setup(s => s.LoadConfigurationAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(_config.Object);
        }

        private string DefaultRoot => Path.Combine(_root, "default");

        private string ConfiguredRoot => Path.Combine(_root, "configured");

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }

        private EventPullService BuildSut(string? mediaRoot = null)
        {
            return new EventPullService(
                _events.Object,
                _deviceRepository.Object,
                _locationRepository.Object,
                _accountRepository.Object,
                mediaDownloadService: _media.Object,
                mediaRoot: mediaRoot,
                storage: _storage.Object,
                configurationService: _configurationService.Object);
        }

        private static string ExpectedFolder(string root)
        {
            return MediaFolderLayout.GetDeviceFolder(root, "Ring", "Home", "Front Door");
        }

        [Fact]
        public async Task PullAccountEventsAsync_ConfiguredMediaRoot_DownloadsIntoConfiguredRoot()
        {
            _ = _config.Setup(c => c.DownloadLocation).Returns(ConfiguredRoot);

            _ = await BuildSut().PullAccountEventsAsync(_accountId, null, null, CancellationToken.None);

            _media.Verify(m => m.DownloadVideosAsync(
                "dev-1", ExpectedFolder(ConfiguredRoot), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
            _media.Verify(m => m.DownloadVideosAsync(
                It.IsAny<string>(), ExpectedFolder(DefaultRoot), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task PullAccountEventsAsync_ChangedMediaRoot_AppliesOnNextPullWithoutRestart()
        {
            EventPullService sut = BuildSut();
            string firstRoot = Path.Combine(_root, "first");
            string secondRoot = Path.Combine(_root, "second");

            _ = _config.Setup(c => c.DownloadLocation).Returns(firstRoot);
            _ = await sut.PullAccountEventsAsync(_accountId, null, null, CancellationToken.None);

            _ = _config.Setup(c => c.DownloadLocation).Returns(secondRoot);
            _ = await sut.PullAccountEventsAsync(_accountId, null, null, CancellationToken.None);

            _media.Verify(m => m.DownloadVideosAsync(
                "dev-1", ExpectedFolder(firstRoot), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
            _media.Verify(m => m.DownloadVideosAsync(
                "dev-1", ExpectedFolder(secondRoot), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task PullAccountEventsAsync_BlankConfiguredRoot_UsesDefaultRoot()
        {
            _ = _config.Setup(c => c.DownloadLocation).Returns((string?)null);

            _ = await BuildSut().PullAccountEventsAsync(_accountId, null, null, CancellationToken.None);

            _media.Verify(m => m.DownloadVideosAsync(
                "dev-1", ExpectedFolder(DefaultRoot), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task PullAccountEventsAsync_ExplicitMediaRootOverride_WinsOverConfiguredRoot()
        {
            _ = _config.Setup(c => c.DownloadLocation).Returns(ConfiguredRoot);
            string overrideRoot = Path.Combine(_root, "override");

            _ = await BuildSut(overrideRoot).PullAccountEventsAsync(_accountId, null, null, CancellationToken.None);

            _media.Verify(m => m.DownloadVideosAsync(
                "dev-1", ExpectedFolder(overrideRoot), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
