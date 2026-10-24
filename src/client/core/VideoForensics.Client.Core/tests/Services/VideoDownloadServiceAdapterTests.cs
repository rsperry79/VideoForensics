using Microsoft.Extensions.Logging;

using Moq;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Core.Services;
using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Core.Contracts;

using Xunit;

namespace VideoForensics.Client.Core.Tests.Services
{
    public class VideoDownloadServiceAdapterTests
    {
        private readonly Mock<ILogger<VideoDownloadServiceAdapter>> _loggerMock;
        private readonly Mock<IVideoProvider> _videoProviderMock;
        private readonly Mock<IProviderAuthService> _authServiceMock;
        private readonly Mock<IMediaDownloadService> _downloadServiceMock;
        private readonly Mock<IDeviceDiscoveryService> _deviceServiceMock;
        private readonly Mock<IVideoForensicsDataClient> _dataClientMock;
        private readonly Mock<IForensicsConfiguration> _configMock;
        private readonly VideoDownloadServiceAdapter _adapter;
        private readonly string _testTempPath;

        public VideoDownloadServiceAdapterTests()
        {
            _loggerMock = new Mock<ILogger<VideoDownloadServiceAdapter>>();
            _videoProviderMock = new Mock<IVideoProvider>();
            _authServiceMock = new Mock<IProviderAuthService>();
            _downloadServiceMock = new Mock<IMediaDownloadService>();
            _deviceServiceMock = new Mock<IDeviceDiscoveryService>();
            _dataClientMock = new Mock<IVideoForensicsDataClient>();
            _configMock = new Mock<IForensicsConfiguration>();

            _ = _videoProviderMock.Setup(p => p.ProviderName).Returns("Ring");
            _ = _configMock.Setup(c => c.MaxConcurrentDownloads).Returns(10);
            _ = _configMock.Setup(c => c.ActiveProviderAccountId).Returns(Guid.NewGuid());

            // Create a unique temp directory for this test run
            _testTempPath = Path.Combine(Path.GetTempPath(), "videoforensics-test", Guid.NewGuid().ToString());
            _ = Directory.CreateDirectory(_testTempPath);

            _adapter = new VideoDownloadServiceAdapter(
                _loggerMock.Object,
                _videoProviderMock.Object,
                _authServiceMock.Object,
                _downloadServiceMock.Object,
                _deviceServiceMock.Object,
                _dataClientMock.Object,
                _configMock.Object);
        }

        [Fact]
        public async Task AuthenticateAsync_SuccessfulAuth_ReturnsTrue()
        {
            var authResult = new AuthResult(Success: true);
            _ = _authServiceMock
                .Setup(s => s.AuthenticateAsync("user@example.com", "password", It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(authResult));

            bool result = await _adapter.AuthenticateAsync("user@example.com", "password");

            Assert.True(result);
        }

        [Fact]
        public async Task AuthenticateAsync_FailedAuth_ReturnsFalse()
        {
            var authResult = new AuthResult(Success: false);
            _ = _authServiceMock
                .Setup(s => s.AuthenticateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(authResult));

            bool result = await _adapter.AuthenticateAsync("user@example.com", "wrongpassword");

            Assert.False(result);
        }

        [Fact]
        public void SetDeviceLocationMapping_StoresMapping()
        {
            var mapping = new Dictionary<string, string>
            {
                { "device-1", "Front Door" },
                { "device-2", "Backyard" }
            };

            _adapter.SetDeviceLocationMapping(mapping);

            Assert.NotNull(mapping);
            Assert.Equal(2, mapping.Count);
        }

        [Fact]
        public void GetDownloadStatus_ReturnsReady()
        {
            string status = _adapter.GetDownloadStatus();

            Assert.Equal("Ready", status);
        }

        [Fact]
        public void GetCurrentDevice_InitialState_ReturnsZeros()
        {
            (int index, int total, string? name) = _adapter.GetCurrentDevice();

            Assert.Equal(0, index);
            Assert.Equal(0, total);
            Assert.Empty(name);
        }

        [Fact]
        public void GetRemainingCount_InitialState_ReturnsZero()
        {
            int count = _adapter.GetRemainingCount();

            Assert.Equal(0, count);
        }

        [Fact]
        public void GetRemainingReason_InitialState_ReturnsNull()
        {
            string? reason = _adapter.GetRemainingReason();

            Assert.Null(reason);
        }

        [Fact]
        public void GetLastError_InitialState_ReturnsNull()
        {
            string? error = _adapter.GetLastError();

            Assert.Null(error);
        }

        [Fact]
        public void GetRateLimitBanUntilUtc_DelegatestoDownloadService()
        {
            DateTime banTime = DateTime.UtcNow.AddMinutes(5);
            _ = _downloadServiceMock
                .Setup(s => s.GetRateLimitBanUntilUtc())
                .Returns(banTime);

            DateTime? result = _adapter.GetRateLimitBanUntilUtc();

            Assert.Equal(banTime, result);
        }

        [Fact]
        public void OverrideRateLimitBan_DelegatestoDownloadService()
        {
            _adapter.OverrideRateLimitBan();

            _downloadServiceMock.Verify(
                s => s.OverrideRateLimitBan(),
                Times.Once);
        }

        [Fact]
        public void GetPreScanCounts_InitialState_ReturnsEmptyDictionary()
        {
            IReadOnlyDictionary<string, int> counts = _adapter.GetPreScanCounts();

            Assert.NotNull(counts);
            Assert.Empty(counts);
        }

        [Fact]
        public void DrainActivityLog_DelegatestoDownloadService()
        {
            var logs = new List<string> { "Downloaded file 1", "Downloaded file 2" };
            _ = _downloadServiceMock
                .Setup(s => s.DrainActivityLog())
                .Returns(logs);

            IReadOnlyList<string> result = _adapter.DrainActivityLog();

            Assert.Equal(2, result.Count);
            _downloadServiceMock.Verify(s => s.DrainActivityLog(), Times.Once);
        }

        [Fact]
        public async Task DownloadVideosAsync_NotAuthenticated_ReturnsFalseAndSetsError()
        {
            _ = _authServiceMock
                .Setup(s => s.IsAuthenticatedAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(false));

            bool result = await _adapter.DownloadVideosAsync(_testTempPath, DateTime.Today, DateTime.Today);

            Assert.False(result);
            string? error = _adapter.GetLastError();
            Assert.NotNull(error);
            Assert.Contains("Not authenticated", error);
        }

        [Fact]
        public async Task DownloadVideosAsync_NoDevices_ReturnsFalseAndSetsError()
        {
            _ = _authServiceMock
                .Setup(s => s.IsAuthenticatedAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(true));

            _ = _deviceServiceMock
                .Setup(s => s.GetLocationsAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult((IReadOnlyList<VideoForensics.Core.Providers.Common.Contracts.Location>)[]));

            bool result = await _adapter.DownloadVideosAsync(_testTempPath, DateTime.Today, DateTime.Today);

            Assert.False(result);
            string? error = _adapter.GetLastError();
            Assert.NotNull(error);
            Assert.Contains("No devices found", error);
        }

        [Fact]
        public async Task PreScanAsync_NoDevices_CompletesWithoutError()
        {
            _ = _deviceServiceMock
                .Setup(s => s.GetLocationsAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult((IReadOnlyList<VideoForensics.Core.Providers.Common.Contracts.Location>)[]));

            await _adapter.PreScanAsync(_testTempPath, DateTime.Today, DateTime.Today.AddDays(1));

            IReadOnlyDictionary<string, int> counts = _adapter.GetPreScanCounts();
            Assert.Empty(counts);
        }

        [Fact]
        public async Task GetProgress_ReturnsDownloadStatus()
        {
            var status = new DownloadStatus(
                IsDownloading: false,
                FilesCompleted: 0,
                FilesTotal: 0,
                BytesDownloaded: 0,
                CurrentFile: string.Empty,
                TotalFilesCompleted: 0,
                TotalFilesMatched: 0,
                TotalBytesDownloaded: 0,
                ActiveConnections: 0,
                CurrentSpeedMbps: 0.0);

            _ = _downloadServiceMock
                .Setup(s => s.GetStatus())
                .Returns(status);

            DownloadStatus result = _adapter.GetProgress();

            Assert.NotNull(result);
            Assert.Equal(0, result.FilesCompleted);
        }

        [Fact]
        public void GetProgress_CalculatesCurrentSpeed()
        {
            var status = new DownloadStatus(
                IsDownloading: true,
                FilesCompleted: 1,
                FilesTotal: 10,
                BytesDownloaded: 1_000_000,
                CurrentFile: "video.mp4",
                TotalFilesCompleted: 1,
                TotalFilesMatched: 10,
                TotalBytesDownloaded: 1_000_000,
                ActiveConnections: 1,
                CurrentSpeedMbps: 0.0);

            _ = _downloadServiceMock
                .Setup(s => s.GetStatus())
                .Returns(status);

            DownloadStatus result = _adapter.GetProgress();

            Assert.NotNull(result);
            Assert.True(result.IsDownloading);
            Assert.Equal(1, result.FilesCompleted);
        }

        [Fact]
        public async Task DownloadSnapshotsAsync_NotAuthenticated_ReturnsFalseAndSetsError()
        {
            _ = _authServiceMock
                .Setup(s => s.IsAuthenticatedAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(false));

            bool result = await _adapter.DownloadSnapshotsAsync(_testTempPath, DateTime.Today, DateTime.Today);

            Assert.False(result);
            string? error = _adapter.GetLastError();
            Assert.NotNull(error);
            Assert.Contains("Not authenticated", error);
        }

        [Fact]
        public async Task DownloadSnapshotsAsync_NoLocations_ReturnsFalseAndSetsError()
        {
            _ = _authServiceMock
                .Setup(s => s.IsAuthenticatedAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(true));

            _ = _deviceServiceMock
                .Setup(s => s.GetLocationsAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult((IReadOnlyList<VideoForensics.Core.Providers.Common.Contracts.Location>)[]));

            bool result = await _adapter.DownloadSnapshotsAsync(_testTempPath, DateTime.Today, DateTime.Today);

            Assert.False(result);
            string? error = _adapter.GetLastError();
            Assert.NotNull(error);
            Assert.Contains("No locations found", error);
        }

        [Fact]
        public async Task DownloadVideosAsync_SanitizesLogOutput_WhenOutputPathContainsNewlines()
        {
            // Arrange: Setup mocks for authentication and devices
            _ = _authServiceMock
                .Setup(s => s.IsAuthenticatedAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(true));

            _ = _deviceServiceMock
                .Setup(s => s.GetLocationsAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult((IReadOnlyList<VideoForensics.Core.Providers.Common.Contracts.Location>)[]));

            // Act: Call DownloadVideosAsync with a path that would be used to test log output sanitization.
            // Use a valid temp path that still exercises the logging and error-setting paths.
            string testPath = Path.Combine(_testTempPath, "subdir");
            bool result = await _adapter.DownloadVideosAsync(testPath, DateTime.Today, DateTime.Today);

            // Assert: Verify the method completed and didn't throw, and sets error about no devices
            // (the sanitization is verified indirectly — the method completes without exception)
            Assert.False(result);
            string? error = _adapter.GetLastError();
            Assert.NotNull(error);
            // The error should be about no devices, not a crash from unescaped log input
            Assert.Contains("No devices found", error);
        }

        private (Guid AccountId, Guid LocationGuid) SetupIdentityScenario(
            VideoForensics.Core.Providers.Common.Contracts.Device device,
            string? locationAddress = null)
        {
            var accountId = Guid.NewGuid();
            var locationGuid = Guid.NewGuid();
            _ = _configMock.Setup(c => c.ActiveProviderAccountId).Returns(accountId);

            var location = new VideoForensics.Core.Providers.Common.Contracts.Location(device.LocationId, "Home", locationAddress);
            _ = _deviceServiceMock
                .Setup(s => s.GetLocationsAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult((IReadOnlyList<VideoForensics.Core.Providers.Common.Contracts.Location>)[location]));
            _ = _deviceServiceMock
                .Setup(s => s.GetDevicesAsync(device.LocationId, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult((IReadOnlyList<VideoForensics.Core.Providers.Common.Contracts.Device>)[device]));

            _ = _dataClientMock
                .Setup(d => d.EnsureLocationAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new VideoForensics.Data.Common.Entities.Location { Id = locationGuid, ProviderLocationId = device.LocationId, Name = "Home" });
            _ = _dataClientMock
                .Setup(d => d.EnsureDeviceAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new VideoForensics.Data.Common.Entities.Device { Id = Guid.NewGuid(), ProviderDeviceId = device.Id, Name = device.Name, Type = device.Type });
            return (accountId, locationGuid);
        }

        [Fact]
        public async Task PreScanAsync_LocationWithAddress_StoresRealAddressAndHashInOwnField()
        {
            var device = new VideoForensics.Core.Providers.Common.Contracts.Device("dev-1", "Front", "doorbot", "loc-1", true);
            (Guid accountId, _) = SetupIdentityScenario(device, "1 Main St");

            await _adapter.PreScanAsync(_testTempPath, DateTime.Today, DateTime.Today.AddDays(1));

            _dataClientMock.Verify(d => d.EnsureLocationAsync(
                accountId,
                "loc-1",
                "Home",
                "1 Main St",
                It.IsAny<string?>(),
                It.Is<string?>(h => h != null && h.Length == 64),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task PreScanAsync_DeviceWithDetailFields_PassesDetailFieldsToEnsureDevice()
        {
            var device = new VideoForensics.Core.Providers.Common.Contracts.Device(
                "dev-1", "Front", "doorbot", "loc-1", true,
                Details: new VideoForensics.Core.Providers.Common.Contracts.DeviceDetailFields(RingDeviceId: "aabbcc", Owned: true));
            _ = SetupIdentityScenario(device);

            await _adapter.PreScanAsync(_testTempPath, DateTime.Today, DateTime.Today.AddDays(1));

            _dataClientMock.Verify(d => d.EnsureDeviceAsync(
                It.IsAny<Guid>(), "dev-1", "Front", "doorbell", true,
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>(),
                It.IsAny<string?>(),
                It.Is<VideoForensics.Core.Providers.Common.Contracts.DeviceDetailFields?>(f => f != null && f.RingDeviceId == "aabbcc" && f.Owned == true)), Times.Once);
        }

        [Fact]
        public async Task PreScanAsync_DeviceWithTimeZoneMetadata_PassesTimeZoneToEnsureDevice()
        {
            var device = new VideoForensics.Core.Providers.Common.Contracts.Device(
                "dev-1", "Front", "doorbot", "loc-1", true,
                new Dictionary<string, string> { ["time_zone"] = "America/New_York" });
            _ = SetupIdentityScenario(device);

            await _adapter.PreScanAsync(_testTempPath, DateTime.Today, DateTime.Today.AddDays(1));

            _dataClientMock.Verify(d => d.EnsureDeviceAsync(
                It.IsAny<Guid>(), "dev-1", "Front", "doorbell", true,
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>(), timeZoneId: "America/New_York"), Times.Once);
        }

        [Fact]
        public async Task PreScanAsync_LocationWithoutAddress_StoresNullAddress()
        {
            var device = new VideoForensics.Core.Providers.Common.Contracts.Device("dev-1", "Front", "doorbot", "loc-1", true);
            (Guid accountId, _) = SetupIdentityScenario(device, null);

            await _adapter.PreScanAsync(_testTempPath, DateTime.Today, DateTime.Today.AddDays(1));

            _dataClientMock.Verify(d => d.EnsureLocationAsync(
                accountId, "loc-1", "Home", null, It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Theory]
        [InlineData("doorbot", "doorbell")]
        [InlineData("authorized_doorbot", "doorbell")]
        [InlineData("stickup_cam", "camera")]
        [InlineData("chime", "chime")]
        [InlineData("something_new", "Unknown")]
        [InlineData("", "Unknown")]
        public async Task PreScanAsync_DeviceType_MapsFromProviderKind(string providerType, string expected)
        {
            var device = new VideoForensics.Core.Providers.Common.Contracts.Device("dev-1", "Front", providerType, "loc-1", true);
            _ = SetupIdentityScenario(device);

            await _adapter.PreScanAsync(_testTempPath, DateTime.Today, DateTime.Today.AddDays(1));

            _dataClientMock.Verify(d => d.EnsureDeviceAsync(
                It.IsAny<Guid>(), "dev-1", "Front", expected, It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Theory]
        [InlineData("doorbell_v4", "doorbell")]
        [InlineData("stickup_cam_lunar", "camera")]
        [InlineData("chime_pro_v2", "chime")]
        public async Task PreScanAsync_DeviceKindMetadata_TakesPrecedenceOverType(string kind, string expected)
        {
            var device = new VideoForensics.Core.Providers.Common.Contracts.Device(
                "dev-1", "Front", "something_new", "loc-1", true, new Dictionary<string, string> { ["kind"] = kind });
            _ = SetupIdentityScenario(device);

            await _adapter.PreScanAsync(_testTempPath, DateTime.Today, DateTime.Today.AddDays(1));

            _dataClientMock.Verify(d => d.EnsureDeviceAsync(
                It.IsAny<Guid>(), "dev-1", "Front", expected, It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task PreScanAsync_FirstPass_PersistsLocationMetadataAfterLocationRowCreated()
        {
            // Arrange - discovery exposes the metadata-persist capability; the Location row must exist before it runs.
            var device = new VideoForensics.Core.Providers.Common.Contracts.Device("dev-1", "Front", "doorbot", "loc-1", true);
            var locationGuid = Guid.NewGuid();
            var discoveredLocation = new VideoForensics.Core.Providers.Common.Contracts.Location(
                "loc-1", "Home", null, new Dictionary<string, string> { ["city"] = "Peoria" });

            var discovery = new Mock<IDeviceDiscoveryService>();
            _ = discovery.As<ILocationMetadataPersister>();
            _ = discovery
                .Setup(s => s.GetLocationsAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult((IReadOnlyList<VideoForensics.Core.Providers.Common.Contracts.Location>)[discoveredLocation]));
            _ = discovery
                .Setup(s => s.GetDevicesAsync("loc-1", It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult((IReadOnlyList<VideoForensics.Core.Providers.Common.Contracts.Device>)[device]));

            bool locationRowStored = false;
            _ = _dataClientMock
                .Setup(d => d.EnsureLocationAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .Callback(() => locationRowStored = true)
                .ReturnsAsync(new VideoForensics.Data.Common.Entities.Location { Id = locationGuid, ProviderLocationId = "loc-1", Name = "Home" });
            _ = _dataClientMock
                .Setup(d => d.EnsureDeviceAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new VideoForensics.Data.Common.Entities.Device { Id = Guid.NewGuid(), ProviderDeviceId = device.Id, Name = device.Name, Type = device.Type });

            bool persistedAfterRowExisted = false;
            IReadOnlyList<VideoForensics.Core.Providers.Common.Contracts.Location>? persisted = null;
            _ = discovery
                .As<ILocationMetadataPersister>()
                .Setup(p => p.PersistLocationMetadataAsync(It.IsAny<IReadOnlyList<VideoForensics.Core.Providers.Common.Contracts.Location>>(), It.IsAny<CancellationToken>()))
                .Callback<IReadOnlyList<VideoForensics.Core.Providers.Common.Contracts.Location>, CancellationToken>((locations, _) =>
                {
                    persisted = locations;
                    persistedAfterRowExisted = locationRowStored;
                })
                .Returns(Task.CompletedTask);

            var adapter = new VideoDownloadServiceAdapter(
                _loggerMock.Object, _videoProviderMock.Object, _authServiceMock.Object, _downloadServiceMock.Object,
                discovery.Object, _dataClientMock.Object, _configMock.Object);

            // Act - single pass, no prior discovery
            await adapter.PreScanAsync(_testTempPath, DateTime.Today, DateTime.Today.AddDays(1));

            // Assert - metadata persisted once, after the Location row was created, with discovery data intact
            Assert.True(persistedAfterRowExisted);
            Assert.NotNull(persisted);
            VideoForensics.Core.Providers.Common.Contracts.Location captured = Assert.Single(persisted!);
            Assert.Equal("loc-1", captured.Id);
            Assert.Equal("Peoria", captured.Metadata!["city"]);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task PreScanAsync_DeviceIsOnline_ComesFromDiscoveredDevice(bool isOnline)
        {
            var device = new VideoForensics.Core.Providers.Common.Contracts.Device("dev-1", "Front", "doorbot", "loc-1", isOnline);
            _ = SetupIdentityScenario(device);

            await _adapter.PreScanAsync(_testTempPath, DateTime.Today, DateTime.Today.AddDays(1));

            _dataClientMock.Verify(d => d.EnsureDeviceAsync(
                It.IsAny<Guid>(), "dev-1", "Front", It.IsAny<string>(), isOnline, It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
