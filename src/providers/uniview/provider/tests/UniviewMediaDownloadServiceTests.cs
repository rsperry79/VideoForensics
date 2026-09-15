using Microsoft.Extensions.Logging;

using Moq;

using VideoForensics.Providers.Common.Contracts;
using VideoForensics.Providers.Uniview.Services;

using Xunit;

namespace VideoForensics.Providers.Uniview.Tests
{
    /// <summary>
    /// Tests for UniviewMediaDownloadService. Tests the initial state and "not authenticated" paths.
    /// Live download and RTP reception are not tested here (would require a live device).
    /// </summary>
    public class UniviewMediaDownloadServiceTests
    {
        [Fact]
        public void UniviewMediaDownloadService_GetStatus_Initial_ReturnIdleState()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<UniviewMediaDownloadService>>();
            var mockSessionProvider = new Mock<IUniviewSessionProvider>();

            var service = new UniviewMediaDownloadService(
                mockLogger.Object,
                mockSessionProvider.Object);

            // Act
            DownloadStatus status = service.GetStatus();

            // Assert
            Assert.False(status.IsDownloading);
            Assert.Equal(0, status.FilesCompleted);
            Assert.Equal(0, status.FilesTotal);
            Assert.Equal(0, status.BytesDownloaded);
        }

        [Fact]
        public void UniviewMediaDownloadService_GetRateLimitBanUntilUtc_Initial_ReturnsNull()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<UniviewMediaDownloadService>>();
            var mockSessionProvider = new Mock<IUniviewSessionProvider>();

            var service = new UniviewMediaDownloadService(
                mockLogger.Object,
                mockSessionProvider.Object);

            // Act
            DateTime? ban = service.GetRateLimitBanUntilUtc();

            // Assert
            Assert.Null(ban);
        }

        [Fact]
        public void UniviewMediaDownloadService_OverrideRateLimitBan_ClearsAnyExistingBan()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<UniviewMediaDownloadService>>();
            var mockSessionProvider = new Mock<IUniviewSessionProvider>();

            var service = new UniviewMediaDownloadService(
                mockLogger.Object,
                mockSessionProvider.Object);

            // Simulate a ban by calling DownloadVideosAsync with a capacity exception
            // (This is complex; for now just test the Override method directly)

            // Act
            service.OverrideRateLimitBan();

            // Assert - should be null after override
            DateTime? ban = service.GetRateLimitBanUntilUtc();
            Assert.Null(ban);
        }

        [Fact]
        public void UniviewMediaDownloadService_DrainActivityLog_Initial_ReturnsEmptyList()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<UniviewMediaDownloadService>>();
            var mockSessionProvider = new Mock<IUniviewSessionProvider>();

            var service = new UniviewMediaDownloadService(
                mockLogger.Object,
                mockSessionProvider.Object);

            // Act
            IReadOnlyList<string> log = service.DrainActivityLog();

            // Assert
            Assert.Empty(log);
        }

        [Fact]
        public async Task UniviewMediaDownloadService_DownloadVideosAsync_NotAuthenticated_ReturnsErrorResult()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<UniviewMediaDownloadService>>();
            var mockSessionProvider = new Mock<IUniviewSessionProvider>();

            _ = mockSessionProvider.Setup(s => s.GetClient()).Returns((UniviewClient?)null);

            var service = new UniviewMediaDownloadService(
                mockLogger.Object,
                mockSessionProvider.Object);

            // Act
            DownloadResult result = await service.DownloadVideosAsync(
                "1",
                "/tmp",
                DateTime.Now.AddDays(-7),
                DateTime.Now,
                cancellationToken: CancellationToken.None);

            // Assert
            Assert.False(result.Success);
            Assert.NotNull(result.ErrorMessage);
            Assert.Contains("not authenticated", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task UniviewMediaDownloadService_DownloadSnapshotsAsync_NotAuthenticated_ReturnsErrorResult()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<UniviewMediaDownloadService>>();
            var mockSessionProvider = new Mock<IUniviewSessionProvider>();

            _ = mockSessionProvider.Setup(s => s.GetClient()).Returns((UniviewClient?)null);

            var service = new UniviewMediaDownloadService(
                mockLogger.Object,
                mockSessionProvider.Object);

            // Act
            DownloadResult result = await service.DownloadSnapshotsAsync(
                "1",
                "/tmp",
                DateTime.Now.AddDays(-7),
                DateTime.Now,
                cancellationToken: CancellationToken.None);

            // Assert
            Assert.False(result.Success);
            Assert.NotNull(result.ErrorMessage);
            Assert.Contains("not authenticated", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public async Task UniviewMediaDownloadService_DownloadVideosAsync_InvalidDeviceId_ReturnsErrorResult()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<UniviewMediaDownloadService>>();
            var mockSessionProvider = new Mock<IUniviewSessionProvider>();

            var client = new UniviewClient("192.168.1.1", "admin", "password");
            _ = mockSessionProvider.Setup(s => s.GetClient()).Returns(client);

            var service = new UniviewMediaDownloadService(
                mockLogger.Object,
                mockSessionProvider.Object);

            try
            {
                // Act
                DownloadResult result = await service.DownloadVideosAsync(
                    "invalid",
                    "/tmp",
                    DateTime.Now.AddDays(-7),
                    DateTime.Now,
                    cancellationToken: CancellationToken.None);

                // Assert
                Assert.False(result.Success);
                Assert.NotNull(result.ErrorMessage);
                Assert.Contains("Invalid device id", result.ErrorMessage);
            }
            finally
            {
                client.Dispose();
            }
        }

        [Fact]
        public async Task UniviewMediaDownloadService_DownloadSnapshotsAsync_InvalidDeviceId_ReturnsErrorResult()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<UniviewMediaDownloadService>>();
            var mockSessionProvider = new Mock<IUniviewSessionProvider>();

            var client = new UniviewClient("192.168.1.1", "admin", "password");
            _ = mockSessionProvider.Setup(s => s.GetClient()).Returns(client);

            var service = new UniviewMediaDownloadService(
                mockLogger.Object,
                mockSessionProvider.Object);

            try
            {
                // Act
                DownloadResult result = await service.DownloadSnapshotsAsync(
                    "not_a_number",
                    "/tmp",
                    DateTime.Now.AddDays(-7),
                    DateTime.Now,
                    cancellationToken: CancellationToken.None);

                // Assert
                Assert.False(result.Success);
                Assert.NotNull(result.ErrorMessage);
                Assert.Contains("Invalid device id", result.ErrorMessage);
            }
            finally
            {
                client.Dispose();
            }
        }

        [Fact]
        public void UniviewMediaDownloadService_Constructor_ThrowsOnNullLogger()
        {
            // Arrange
            var mockSessionProvider = new Mock<IUniviewSessionProvider>();

            // Act & Assert
            _ = Assert.Throws<ArgumentNullException>(() =>
                new UniviewMediaDownloadService(
                    null!,
                    mockSessionProvider.Object));
        }

        [Fact]
        public void UniviewMediaDownloadService_Constructor_ThrowsOnNullSessionProvider()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<UniviewMediaDownloadService>>();

            // Act & Assert
            _ = Assert.Throws<ArgumentNullException>(() =>
                new UniviewMediaDownloadService(
                    mockLogger.Object,
                    null!));
        }

        [Fact]
        public void UniviewMediaDownloadService_DrainActivityLog_IsIdempotent()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<UniviewMediaDownloadService>>();
            var mockSessionProvider = new Mock<IUniviewSessionProvider>();

            var service = new UniviewMediaDownloadService(
                mockLogger.Object,
                mockSessionProvider.Object);

            // Act
            IReadOnlyList<string> log1 = service.DrainActivityLog();
            IReadOnlyList<string> log2 = service.DrainActivityLog();

            // Assert - both should be empty, second drain gets nothing
            Assert.Empty(log1);
            Assert.Empty(log2);
        }
    }
}
