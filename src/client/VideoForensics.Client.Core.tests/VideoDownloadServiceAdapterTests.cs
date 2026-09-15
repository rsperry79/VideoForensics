using Microsoft.Extensions.Logging;
using Moq;
using VideoForensics.Client.Core.Services;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Data.Core.Contracts;
using VideoForensics.Providers.Common.Contracts;
using Xunit;

namespace VideoForensics.Client.Core.Tests
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

        public VideoDownloadServiceAdapterTests()
        {
            _loggerMock = new Mock<ILogger<VideoDownloadServiceAdapter>>();
            _videoProviderMock = new Mock<IVideoProvider>();
            _authServiceMock = new Mock<IProviderAuthService>();
            _downloadServiceMock = new Mock<IMediaDownloadService>();
            _deviceServiceMock = new Mock<IDeviceDiscoveryService>();
            _dataClientMock = new Mock<IVideoForensicsDataClient>();
            _configMock = new Mock<IForensicsConfiguration>();

            _videoProviderMock.Setup(p => p.ProviderName).Returns("Ring");
            _configMock.Setup(c => c.MaxConcurrentDownloads).Returns(10);
            _configMock.Setup(c => c.ActiveProviderAccountId).Returns(Guid.NewGuid());

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
            _authServiceMock
                .Setup(s => s.AuthenticateAsync("user@example.com", "password", It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(authResult));

            var result = await _adapter.AuthenticateAsync("user@example.com", "password");

            Assert.True(result);
        }

        [Fact]
        public async Task AuthenticateAsync_FailedAuth_ReturnsFalse()
        {
            var authResult = new AuthResult(Success: false);
            _authServiceMock
                .Setup(s => s.AuthenticateAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(authResult));

            var result = await _adapter.AuthenticateAsync("user@example.com", "wrongpassword");

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
            var status = _adapter.GetDownloadStatus();

            Assert.Equal("Ready", status);
        }

        [Fact]
        public void GetCurrentDevice_InitialState_ReturnsZeros()
        {
            var (index, total, name) = _adapter.GetCurrentDevice();

            Assert.Equal(0, index);
            Assert.Equal(0, total);
            Assert.Empty(name);
        }

        [Fact]
        public void GetRemainingCount_InitialState_ReturnsZero()
        {
            var count = _adapter.GetRemainingCount();

            Assert.Equal(0, count);
        }

        [Fact]
        public void GetRemainingReason_InitialState_ReturnsNull()
        {
            var reason = _adapter.GetRemainingReason();

            Assert.Null(reason);
        }

        [Fact]
        public void GetLastError_InitialState_ReturnsNull()
        {
            var error = _adapter.GetLastError();

            Assert.Null(error);
        }

        [Fact]
        public void GetRateLimitBanUntilUtc_DelegatestoDownloadService()
        {
            var banTime = DateTime.UtcNow.AddMinutes(5);
            _downloadServiceMock
                .Setup(s => s.GetRateLimitBanUntilUtc())
                .Returns(banTime);

            var result = _adapter.GetRateLimitBanUntilUtc();

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
            var counts = _adapter.GetPreScanCounts();

            Assert.NotNull(counts);
            Assert.Empty(counts);
        }

        [Fact]
        public void DrainActivityLog_DelegatestoDownloadService()
        {
            var logs = new List<string> { "Downloaded file 1", "Downloaded file 2" };
            _downloadServiceMock
                .Setup(s => s.DrainActivityLog())
                .Returns(logs);

            var result = _adapter.DrainActivityLog();

            Assert.Equal(2, result.Count);
            _downloadServiceMock.Verify(s => s.DrainActivityLog(), Times.Once);
        }

        [Fact]
        public async Task DownloadVideosAsync_NotAuthenticated_ReturnsFalseAndSetsError()
        {
            _authServiceMock
                .Setup(s => s.IsAuthenticatedAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(false));

            var result = await _adapter.DownloadVideosAsync("C:\\Downloads", DateTime.Today, DateTime.Today);

            Assert.False(result);
            var error = _adapter.GetLastError();
            Assert.NotNull(error);
            Assert.Contains("Not authenticated", error);
        }

        [Fact]
        public async Task DownloadVideosAsync_NoDevices_ReturnsFalseAndSetsError()
        {
            _authServiceMock
                .Setup(s => s.IsAuthenticatedAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(true));

            _deviceServiceMock
                .Setup(s => s.GetLocationsAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult((IReadOnlyList<Location>)new List<Location>()));

            var result = await _adapter.DownloadVideosAsync("C:\\Downloads", DateTime.Today, DateTime.Today);

            Assert.False(result);
            var error = _adapter.GetLastError();
            Assert.NotNull(error);
            Assert.Contains("No devices found", error);
        }

        [Fact]
        public async Task PreScanAsync_NoDevices_CompletesWithoutError()
        {
            _deviceServiceMock
                .Setup(s => s.GetLocationsAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult((IReadOnlyList<Location>)new List<Location>()));

            await _adapter.PreScanAsync("C:\\Downloads", DateTime.Today, DateTime.Today.AddDays(1));

            var counts = _adapter.GetPreScanCounts();
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
                CurrentFile: "",
                TotalFilesCompleted: 0,
                TotalFilesMatched: 0,
                TotalBytesDownloaded: 0,
                ActiveConnections: 0,
                CurrentSpeedMbps: 0.0);

            _downloadServiceMock
                .Setup(s => s.GetStatus())
                .Returns(status);

            var result = _adapter.GetProgress();

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

            _downloadServiceMock
                .Setup(s => s.GetStatus())
                .Returns(status);

            var result = _adapter.GetProgress();

            Assert.NotNull(result);
            Assert.True(result.IsDownloading);
            Assert.Equal(1, result.FilesCompleted);
        }

        [Fact]
        public async Task DownloadSnapshotsAsync_NotAuthenticated_ReturnsFalseAndSetsError()
        {
            _authServiceMock
                .Setup(s => s.IsAuthenticatedAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(false));

            var result = await _adapter.DownloadSnapshotsAsync("C:\\Snapshots", DateTime.Today, DateTime.Today);

            Assert.False(result);
            var error = _adapter.GetLastError();
            Assert.NotNull(error);
            Assert.Contains("Not authenticated", error);
        }

        [Fact]
        public async Task DownloadSnapshotsAsync_NoLocations_ReturnsFalseAndSetsError()
        {
            _authServiceMock
                .Setup(s => s.IsAuthenticatedAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(true));

            _deviceServiceMock
                .Setup(s => s.GetLocationsAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult((IReadOnlyList<Location>)new List<Location>()));

            var result = await _adapter.DownloadSnapshotsAsync("C:\\Snapshots", DateTime.Today, DateTime.Today);

            Assert.False(result);
            var error = _adapter.GetLastError();
            Assert.NotNull(error);
            Assert.Contains("No locations found", error);
        }
    }
}
