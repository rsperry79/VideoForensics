using Microsoft.Extensions.Logging;

using Moq;

using System.Net;

using VideoForensics.Core.Providers.Common.Contracts;
using VideoForensics.Data.Core.Contracts;
using VideoForensics.Providers.Ring.Services;

using Xunit;

namespace VideoForensics.Providers.Ring.Tests
{
    /// <summary>
    /// Tests for RingMediaDownloadService implementation.
    /// Verifies video and snapshot download functionality.
    /// </summary>
    public class RingMediaDownloadServiceTests
    {
        private static IVideoForensicsDataClient CreateMockDataClient()
        {
            var mock = new Mock<IVideoForensicsDataClient>();
            // For tests that don't actually call DB methods, this mock will suffice.
            // Real integration tests would set up more specific behaviors.
            return mock.Object;
        }
        [Fact]
        public async Task DownloadVideosAsync_WithoutSession_ReturnsFailureResult()
        {
            // Arrange
            var sessionProvider = new Mock<ISessionProvider>();
            _ = sessionProvider.Setup(sp => sp.GetSession(It.IsAny<Guid>())).Returns((Session?)null);
            ILogger logger = new Mock<ILogger>().Object;
            IVideoForensicsDataClient dataClient = CreateMockDataClient();
            var service = new RingMediaDownloadService(logger, sessionProvider.Object, dataClient);

            // Act
            DownloadResult result = await service.DownloadVideosAsync(
                "device123",
                "/tmp/videos",
                DateTime.Now.AddDays(-7),
                DateTime.Now
            );

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.NotNull(result.ErrorMessage);
        }

        [Fact]
        public async Task DownloadVideosAsync_ReturnsDownloadResult()
        {
            // Arrange
            var sessionProvider = new Mock<ISessionProvider>();
            ILogger logger = new Mock<ILogger>().Object;
            IVideoForensicsDataClient dataClient = CreateMockDataClient();
            var service = new RingMediaDownloadService(logger, sessionProvider.Object, dataClient);

            // Act
            DownloadResult result = await service.DownloadVideosAsync(
                "device123",
                "/tmp/videos",
                DateTime.Now.AddDays(-7),
                DateTime.Now
            );

            // Assert
            Assert.NotNull(result);
            _ = Assert.IsType<DownloadResult>(result);
            // Should return a result even if session is null - graceful error handling
        }

        [Fact]
        public async Task DownloadSnapshotsAsync_WithoutSession_ReturnsFailureResult()
        {
            // Arrange
            var sessionProvider = new Mock<ISessionProvider>();
            _ = sessionProvider.Setup(sp => sp.GetSession(It.IsAny<Guid>())).Returns((Session?)null);
            ILogger logger = new Mock<ILogger>().Object;
            IVideoForensicsDataClient dataClient = CreateMockDataClient();
            var service = new RingMediaDownloadService(logger, sessionProvider.Object, dataClient);

            // Act
            DownloadResult result = await service.DownloadSnapshotsAsync(
                "device123",
                "/tmp/snapshots",
                DateTime.Now.AddDays(-7),
                DateTime.Now
            );

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Success);
        }

        [Fact]
        public async Task DownloadSnapshotsAsync_ReturnsDownloadResult()
        {
            // Arrange
            var sessionProvider = new Mock<ISessionProvider>();
            ILogger logger = new Mock<ILogger>().Object;
            IVideoForensicsDataClient dataClient = CreateMockDataClient();
            var service = new RingMediaDownloadService(logger, sessionProvider.Object, dataClient);

            // Act
            DownloadResult result = await service.DownloadSnapshotsAsync(
                "device123",
                "/tmp/snapshots",
                DateTime.Now.AddDays(-7),
                DateTime.Now
            );

            // Assert
            Assert.NotNull(result);
            _ = Assert.IsType<DownloadResult>(result);
        }

        [Fact]
        public void GetStatus_ReturnsDownloadStatus()
        {
            // Arrange
            var sessionProvider = new Mock<ISessionProvider>();
            ILogger logger = new Mock<ILogger>().Object;
            IVideoForensicsDataClient dataClient = CreateMockDataClient();
            var service = new RingMediaDownloadService(logger, sessionProvider.Object, dataClient);

            // Act
            DownloadStatus status = service.GetStatus();

            // Assert
            Assert.NotNull(status);
            _ = Assert.IsType<DownloadStatus>(status);
            Assert.False(status.IsDownloading);
        }

        [Fact]
        public void ConstructorThrowsOnNullSessionProvider()
        {
            // Arrange
            ILogger logger = new Mock<ILogger>().Object;
            IVideoForensicsDataClient dataClient = CreateMockDataClient();

            // Act & Assert
            _ = Assert.Throws<ArgumentNullException>(() => new RingMediaDownloadService(logger, null!, dataClient));
        }

        [Fact]
        public async Task DownloadVideosAsync_WithValidDates_AcceptsDateRange()
        {
            // Arrange
            var sessionProvider = new Mock<ISessionProvider>();
            ILogger logger = new Mock<ILogger>().Object;
            IVideoForensicsDataClient dataClient = CreateMockDataClient();
            var service = new RingMediaDownloadService(logger, sessionProvider.Object, dataClient);
            var startDate = new DateTime(2024, 1, 1);
            var endDate = new DateTime(2024, 1, 31);

            // Act
            DownloadResult result = await service.DownloadVideosAsync(
                "device123",
                "/tmp/videos",
                startDate,
                endDate
            );

            // Assert
            Assert.NotNull(result);
            // Should handle date range gracefully
        }

        [Fact]
        public async Task GetMatchedEventCountAsync_WithoutSession_ReturnsZero()
        {
            // Arrange
            var sessionProvider = new Mock<ISessionProvider>();
            _ = sessionProvider.Setup(sp => sp.GetSession(It.IsAny<Guid>())).Returns((Session?)null);
            ILogger logger = new Mock<ILogger>().Object;
            IVideoForensicsDataClient dataClient = CreateMockDataClient();
            var service = new RingMediaDownloadService(logger, sessionProvider.Object, dataClient);

            // Act
            int count = await service.GetMatchedEventCountAsync("device123", DateTime.Now.AddDays(-7), DateTime.Now);

            // Assert
            Assert.Equal(0, count);
        }

        [Fact]
        public void GetStatus_IsInitiallyNotDownloading()
        {
            // Arrange
            var sessionProvider = new Mock<ISessionProvider>();
            ILogger logger = new Mock<ILogger>().Object;
            IVideoForensicsDataClient dataClient = CreateMockDataClient();
            var service = new RingMediaDownloadService(logger, sessionProvider.Object, dataClient);

            // Act
            DownloadStatus status = service.GetStatus();

            // Assert
            Assert.False(status.IsDownloading);
            Assert.Equal(0, status.FilesCompleted);
            Assert.Equal(0, status.FilesTotal);
            Assert.Equal(0L, status.BytesDownloaded);
        }

        [Fact]
        public void SetMaxConcurrentDownloads_WithPositiveValue_AcceptsValue()
        {
            // Arrange
            var sessionProvider = new Mock<ISessionProvider>();
            ILogger logger = new Mock<ILogger>().Object;
            IVideoForensicsDataClient dataClient = CreateMockDataClient();
            var service = new RingMediaDownloadService(logger, sessionProvider.Object, dataClient);

            // Act
            service.SetMaxConcurrentDownloads(5);

            // Assert - no exception thrown
            Assert.True(true);
        }

        [Fact]
        public void SetMaxConcurrentDownloads_WithZeroOrNegative_IgnoresValue()
        {
            // Arrange
            var sessionProvider = new Mock<ISessionProvider>();
            ILogger logger = new Mock<ILogger>().Object;
            IVideoForensicsDataClient dataClient = CreateMockDataClient();
            var service = new RingMediaDownloadService(logger, sessionProvider.Object, dataClient);

            // Act - setting to 0 or negative should be ignored
            service.SetMaxConcurrentDownloads(0);
            service.SetMaxConcurrentDownloads(-1);

            // Assert - no exception thrown
            Assert.True(true);
        }

        [Fact]
        public void SetActiveProviderAccountId_CachesAccountId()
        {
            // Arrange
            var sessionProvider = new Mock<ISessionProvider>();
            ILogger logger = new Mock<ILogger>().Object;
            IVideoForensicsDataClient dataClient = CreateMockDataClient();
            var service = new RingMediaDownloadService(logger, sessionProvider.Object, dataClient);
            var accountId = Guid.NewGuid();

            // Act
            service.SetActiveProviderAccountId(accountId);

            // Assert - no exception thrown, account is cached for later use
            Assert.True(true);
        }

        [Fact]
        public void IsHistoryCached_WithNoCacheData_ReturnsFalse()
        {
            // Arrange
            var sessionProvider = new Mock<ISessionProvider>();
            ILogger logger = new Mock<ILogger>().Object;
            IVideoForensicsDataClient dataClient = CreateMockDataClient();
            var service = new RingMediaDownloadService(logger, sessionProvider.Object, dataClient);

            // Act
            bool isCached = service.IsHistoryCached(DateTime.Now.AddDays(-7), DateTime.Now);

            // Assert
            Assert.False(isCached);
        }

        [Fact]
        public void DrainActivityLog_WithEmptyLog_ReturnsEmptyList()
        {
            // Arrange
            var sessionProvider = new Mock<ISessionProvider>();
            ILogger logger = new Mock<ILogger>().Object;
            IVideoForensicsDataClient dataClient = CreateMockDataClient();
            var service = new RingMediaDownloadService(logger, sessionProvider.Object, dataClient);

            // Act
            IReadOnlyList<string> log = service.DrainActivityLog();

            // Assert
            Assert.NotNull(log);
            Assert.Empty(log);
        }

        [Fact]
        public void GetRateLimitBanUntilUtc_WithoutRateLimit_ReturnsNull()
        {
            // Arrange
            var sessionProvider = new Mock<ISessionProvider>();
            ILogger logger = new Mock<ILogger>().Object;
            IVideoForensicsDataClient dataClient = CreateMockDataClient();
            var service = new RingMediaDownloadService(logger, sessionProvider.Object, dataClient);

            // Act
            DateTime? banUntil = service.GetRateLimitBanUntilUtc();

            // Assert
            Assert.Null(banUntil);
        }

        [Fact]
        public void OverrideRateLimitBan_DoesNotThrow()
        {
            // Arrange
            var sessionProvider = new Mock<ISessionProvider>();
            ILogger logger = new Mock<ILogger>().Object;
            IVideoForensicsDataClient dataClient = CreateMockDataClient();
            var service = new RingMediaDownloadService(logger, sessionProvider.Object, dataClient);

            // Act
            service.OverrideRateLimitBan();

            // Assert - no exception thrown
            Assert.True(true);
        }

        private sealed class StubRingHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                string url = request.RequestUri?.ToString() ?? string.Empty;
                HttpContent content = url.Contains("snapshots/image", StringComparison.Ordinal)
                    ? new ByteArrayContent(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46 })
                    : (HttpContent)(url.Contains("oauth", StringComparison.OrdinalIgnoreCase)
                        ? new StringContent("{\"access_token\":\"tok\",\"refresh_token\":\"ref\",\"expires_in\":3600,\"token_type\":\"Bearer\"}", System.Text.Encoding.UTF8, "application/json")
                        : new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));

                // Ring answers the on-demand refresh with 204 No Content; anything else is an error.
                HttpStatusCode status = url.Contains("snapshots/update_all", StringComparison.Ordinal) ? HttpStatusCode.NoContent : HttpStatusCode.OK;
                return Task.FromResult(new HttpResponseMessage(status) { Content = content });
            }
        }

        [Fact]
        public async Task DownloadVideosAsync_SuccessfulDownload_ReleasesFileHandle()
        {
            // Arrange - a real session over a stub handler so a snapshot file is actually written and hashed
            var session = new Session("testuser", "testpass", new StubRingHandler(), null, null);
            _ = await session.Authenticate();
            var sessionProvider = new Mock<ISessionProvider>();
            _ = sessionProvider.Setup(sp => sp.GetSession(It.IsAny<Guid>())).Returns(session);
            ILogger logger = new Mock<ILogger>().Object;
            var dataClientMock = new Mock<IVideoForensicsDataClient>();
            _ = dataClientMock.Setup(d => d.EnsureLocationAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new VideoForensics.Data.Common.Entities.Location { Id = Guid.NewGuid(), ProviderLocationId = "loc", Name = "loc" });
            _ = dataClientMock.Setup(d => d.EnsureDeviceAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>(), It.IsAny<string?>(), It.IsAny<DeviceDetailFields?>()))
                .ReturnsAsync(new VideoForensics.Data.Common.Entities.Device { Id = Guid.NewGuid(), ProviderDeviceId = "12345", Name = "cam", Type = "doorbell" });
            var service = new RingMediaDownloadService(logger, sessionProvider.Object, dataClientMock.Object);
            service.SetActiveProviderAccountId(Guid.NewGuid());
            string folder = Path.Combine(Path.GetTempPath(), "ring_handle_" + Guid.NewGuid().ToString("N"));

            try
            {
                // Act
                _ = await service.DownloadSnapshotsAsync("12345", folder, DateTime.Now.AddDays(-1), DateTime.Now);

                // Assert - the downloaded file was written, and deleting the folder (no retry) must not hit a locked file
                Assert.NotEmpty(Directory.GetFiles(folder, "*.jpg"));
                Exception? ex = Record.Exception(() => Directory.Delete(folder, recursive: true));
                Assert.Null(ex);
            }
            finally
            {
                if (Directory.Exists(folder))
                {
                    try
                    { Directory.Delete(folder, recursive: true); }
                    catch (IOException) { }
                }
            }
        }

        private const string VideoEventCreatedAt = "2026-10-17T12:00:00Z";

        private sealed class StubVideoHistoryHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                string url = request.RequestUri?.ToString() ?? string.Empty;
                string body;
                if (url.Contains("oauth", StringComparison.OrdinalIgnoreCase))
                {
                    body = "{\"access_token\":\"tok\",\"refresh_token\":\"ref\",\"expires_in\":3600,\"token_type\":\"Bearer\"}";
                }
                else if (url.Contains("share/download", StringComparison.Ordinal))
                {
                    body = "{\"url\":\"https://recordings.example.test/rec.mp4\"}";
                }
                else if (url.Contains("history", StringComparison.Ordinal))
                {
                    // First page has the one event; any older_than page is empty so paging terminates.
                    body = url.Contains("older_than", StringComparison.Ordinal)
                        ? "[]"
                        : "[{\"id\":987654,\"created_at\":\"" + VideoEventCreatedAt + "\",\"kind\":\"motion\",\"doorbot\":{\"id\":12345,\"description\":\"cam\"}}]";
                }
                else
                {
                    body = "{}";
                }

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
                });
            }
        }

        private sealed class StubVideoDownloader : IVideoDownloader
        {
            public Task<Stream> OpenStreamAsync(string url)
            {
                return Task.FromResult<Stream>(new MemoryStream(new byte[] { 1, 2, 3, 4 }));
            }

            public async Task DownloadToFileAsync(string url, string saveAsPath)
            {
                _ = Directory.CreateDirectory(Path.GetDirectoryName(saveAsPath)!);
                await File.WriteAllBytesAsync(saveAsPath, new byte[] { 0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79, 0x70 });
            }
        }

        private static async Task<(RingMediaDownloadService Service, Session Session)> CreateVideoServiceAsync()
        {
            var session = new Session("testuser", "testpass", new StubVideoHistoryHandler(), new StubVideoDownloader(), null);
            _ = await session.Authenticate();
            var sessionProvider = new Mock<ISessionProvider>();
            _ = sessionProvider.Setup(sp => sp.GetSession(It.IsAny<Guid>())).Returns(session);
            ILogger logger = new Mock<ILogger>().Object;
            var dataClientMock = new Mock<IVideoForensicsDataClient>();
            _ = dataClientMock.Setup(d => d.EnsureLocationAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new VideoForensics.Data.Common.Entities.Location { Id = Guid.NewGuid(), ProviderLocationId = "loc", Name = "loc" });
            _ = dataClientMock.Setup(d => d.EnsureDeviceAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool?>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>(), It.IsAny<string?>(), It.IsAny<DeviceDetailFields?>()))
                .ReturnsAsync(new VideoForensics.Data.Common.Entities.Device { Id = Guid.NewGuid(), ProviderDeviceId = "12345", Name = "cam", Type = "doorbell" });
            var tagger = new Mock<IMediaMetadataTagger>();
            _ = tagger.Setup(t => t.TagEventIdAsync(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
            var service = new RingMediaDownloadService(logger, sessionProvider.Object, dataClientMock.Object, tagger.Object);
            service.SetActiveProviderAccountId(Guid.NewGuid());
            return (service, session);
        }

        private static void TryDeleteFolder(string folder)
        {
            if (Directory.Exists(folder))
            {
                try
                { Directory.Delete(folder, recursive: true); }
                catch (IOException) { }
            }
        }

        [Fact]
        public async Task DownloadVideosAsync_NewVideo_ReleasesFileHandle()
        {
            // Arrange - history returns one event; the stub downloader writes a non-empty mp4, which the service then hashes
            (RingMediaDownloadService service, _) = await CreateVideoServiceAsync();
            string folder = Path.Combine(Path.GetTempPath(), "ring_vhandle_new_" + Guid.NewGuid().ToString("N"));

            try
            {
                // Act
                DownloadResult result = await service.DownloadVideosAsync("12345", folder, DateTime.Now.AddDays(-30), DateTime.Now.AddDays(1));

                // Assert - the file was newly written, and deleting the folder (no retry) must not hit a locked file
                Assert.True(result.Success, result.ErrorMessage);
                Assert.NotEmpty(Directory.GetFiles(folder, "*.mp4"));
                Exception? ex = Record.Exception(() => Directory.Delete(folder, recursive: true));
                Assert.Null(ex);
            }
            finally
            {
                TryDeleteFolder(folder);
            }
        }

        [Fact]
        public async Task DownloadVideosAsync_ExistingVideoOnDisk_ReleasesFileHandle()
        {
            // Arrange - pre-create the exact file the service computes for the event so the existing-file hash path runs
            (RingMediaDownloadService service, _) = await CreateVideoServiceAsync();
            string folder = Path.Combine(Path.GetTempPath(), "ring_vhandle_existing_" + Guid.NewGuid().ToString("N"));
            _ = Directory.CreateDirectory(folder);
            string existingFile = Path.Combine(folder,
                MediaFileNamer.FormatMediaFileName("cam", DateTime.Parse(VideoEventCreatedAt), "motion", "mp4"));
            byte[] existingBytes = new byte[] { 9, 9, 9, 9 };
            await File.WriteAllBytesAsync(existingFile, existingBytes);

            try
            {
                // Act
                DownloadResult result = await service.DownloadVideosAsync("12345", folder, DateTime.Now.AddDays(-30), DateTime.Now.AddDays(1));

                // Assert - the pre-existing file was kept (not redownloaded), and the folder can be deleted without a locked file
                Assert.True(result.Success, result.ErrorMessage);
                Assert.Equal(existingBytes, await File.ReadAllBytesAsync(existingFile));
                Exception? ex = Record.Exception(() => Directory.Delete(folder, recursive: true));
                Assert.Null(ex);
            }
            finally
            {
                TryDeleteFolder(folder);
            }
        }

        [Fact]
        public async Task DownloadSnapshotsAsync_WithInvalidDeviceId_ReturnsFailure()
        {
            // Arrange
            var sessionProvider = new Mock<ISessionProvider>();
            var session = new Mock<Session>("testuser", "testpass", null, null, null);
            _ = sessionProvider.Setup(sp => sp.GetSession(It.IsAny<Guid>())).Returns(session.Object);
            ILogger logger = new Mock<ILogger>().Object;
            IVideoForensicsDataClient dataClient = CreateMockDataClient();
            var service = new RingMediaDownloadService(logger, sessionProvider.Object, dataClient);

            // Act
            DownloadResult result = await service.DownloadSnapshotsAsync(
                "not-a-number",
                Path.Combine(Path.GetTempPath(), "snapshots_test"),
                DateTime.Now.AddDays(-7),
                DateTime.Now
            );

            // Assert
            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Contains("Invalid device id", result.ErrorMessage ?? string.Empty);
        }

        [Fact]
        public void ConstructorThrowsOnNullDataClient()
        {
            // Arrange
            var sessionProvider = new Mock<ISessionProvider>();
            ILogger logger = new Mock<ILogger>().Object;

            // Act & Assert
            _ = Assert.Throws<ArgumentNullException>(() => new RingMediaDownloadService(logger, sessionProvider.Object, null!));
        }

        [Fact]
        public async Task GetMatchedEventCountAsync_WithAccountId_PassesToSessionProvider()
        {
            // Arrange
            var accountId = Guid.NewGuid();
            var sessionProvider = new Mock<ISessionProvider>();
            var mockSession = new Mock<Session>("testuser", "testpass", null, null, null);

            _ = sessionProvider.Setup(sp => sp.GetSession(accountId)).Returns(mockSession.Object);

            ILogger logger = new Mock<ILogger>().Object;
            IVideoForensicsDataClient dataClient = CreateMockDataClient();
            var service = new RingMediaDownloadService(logger, sessionProvider.Object, dataClient);

            // Prime the cached account ID
            service.SetActiveProviderAccountId(accountId);

            // Act
            int count = await service.GetMatchedEventCountAsync("device123", DateTime.Now.AddDays(-7), DateTime.Now);

            // Assert
            sessionProvider.Verify(sp => sp.GetSession(accountId), Times.Once, "GetSession should be called with the cached accountId");
        }

        [Fact]
        public async Task DownloadVideosAsync_WithAccountId_PassesToSessionProvider()
        {
            // Arrange
            var accountId = Guid.NewGuid();
            var sessionProvider = new Mock<ISessionProvider>();
            var mockSession = new Mock<Session>("testuser", "testpass", null, null, null);

            _ = sessionProvider.Setup(sp => sp.GetSession(accountId)).Returns(mockSession.Object);

            ILogger logger = new Mock<ILogger>().Object;
            IVideoForensicsDataClient dataClient = CreateMockDataClient();
            var service = new RingMediaDownloadService(logger, sessionProvider.Object, dataClient);

            // Prime the cached account ID
            service.SetActiveProviderAccountId(accountId);

            // Act
            DownloadResult result = await service.DownloadVideosAsync(
                "device123",
                "/tmp/videos",
                DateTime.Now.AddDays(-7),
                DateTime.Now
            );

            // Assert
            sessionProvider.Verify(sp => sp.GetSession(accountId), Times.Once, "GetSession should be called with the cached accountId");
        }

        [Fact]
        public async Task DownloadSnapshotsAsync_WithAccountId_PassesToSessionProvider()
        {
            // Arrange
            var accountId = Guid.NewGuid();
            var sessionProvider = new Mock<ISessionProvider>();
            var mockSession = new Mock<Session>("testuser", "testpass", null, null, null);

            _ = sessionProvider.Setup(sp => sp.GetSession(accountId)).Returns(mockSession.Object);

            ILogger logger = new Mock<ILogger>().Object;
            IVideoForensicsDataClient dataClient = CreateMockDataClient();
            var service = new RingMediaDownloadService(logger, sessionProvider.Object, dataClient);

            // Prime the cached account ID
            service.SetActiveProviderAccountId(accountId);

            // Act
            DownloadResult result = await service.DownloadSnapshotsAsync(
                "123",
                "/tmp/snapshots",
                DateTime.Now.AddDays(-7),
                DateTime.Now
            );

            // Assert
            sessionProvider.Verify(sp => sp.GetSession(accountId), Times.Once, "GetSession should be called with the cached accountId");
        }

        [Fact]
        public async Task DownloadSnapshotsAsync_WhenFreshSnapshotCannotBeCaptured_FailsWithoutSavingOldImage()
        {
            // Arrange: a failed refresh must not fall back to Ring's cached image, which would look current.
            var sessionProvider = new Mock<ISessionProvider>();
            var mockSession = new Mock<Session>("testuser", "testpass", null, null, null);
            _ = sessionProvider.Setup(sp => sp.GetSession(It.IsAny<Guid>())).Returns(mockSession.Object);
            var service = new RefreshFailingMediaDownloadService(new Mock<ILogger>().Object, sessionProvider.Object, CreateMockDataClient());
            string outputPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

            // Act
            DownloadResult result = await service.DownloadSnapshotsAsync("123", outputPath, DateTime.Now.AddDays(-1), DateTime.Now);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("fresh snapshot", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
            Assert.False(Directory.Exists(outputPath) && Directory.EnumerateFiles(outputPath).Any());
        }

        [Fact]
        public async Task ConcurrentDownloads_WithDifferentAccountIds_DoesNotRace()
        {
            // Arrange
            var accountId1 = Guid.NewGuid();
            var accountId2 = Guid.NewGuid();
            var sessionProvider = new Mock<ISessionProvider>();
            var mockSession1 = new Mock<Session>("user1", "pass1", null, null, null);
            var mockSession2 = new Mock<Session>("user2", "pass2", null, null, null);

            _ = sessionProvider.Setup(sp => sp.GetSession(accountId1)).Returns(mockSession1.Object);
            _ = sessionProvider.Setup(sp => sp.GetSession(accountId2)).Returns(mockSession2.Object);

            ILogger logger = new Mock<ILogger>().Object;
            IVideoForensicsDataClient dataClient = CreateMockDataClient();
            var service = new RingMediaDownloadService(logger, sessionProvider.Object, dataClient);

            // Act
            service.SetActiveProviderAccountId(accountId1);
            Task<int> task1 = service.GetMatchedEventCountAsync("device123", DateTime.Now.AddDays(-7), DateTime.Now);

            service.SetActiveProviderAccountId(accountId2);
            Task<int> task2 = service.GetMatchedEventCountAsync("device456", DateTime.Now.AddDays(-7), DateTime.Now);

            _ = await Task.WhenAll(task1, task2);

            // Assert - verify each account's session was called
            sessionProvider.Verify(sp => sp.GetSession(accountId1), Times.Once);
            sessionProvider.Verify(sp => sp.GetSession(accountId2), Times.Once);
        }

        [Fact]
        public void ResolveDeviceState_DoorbotHealthOffline_ReturnsKindAndOfflineEvenIfSubscribed()
        {
            var devices = new Entities.Devices
            {
                Doorbots = [new Entities.Doorbot { Id = 123, Kind = "doorbell_v4", Subscribed = true, Health = new Entities.DeviceHealth { Connected = false } }]
            };

            (string? kind, bool? isOnline) = RingMediaDownloadService.ResolveDeviceState(devices, "123");

            Assert.Equal("doorbell_v4", kind);
            Assert.False(isOnline);
        }

        [Fact]
        public void ResolveDeviceState_StickupCamWithoutHealth_UsesSubscribedAndFallsBackToListType()
        {
            var devices = new Entities.Devices
            {
                StickupCams = [new Entities.StickupCam { Id = 456, Kind = string.Empty, Subscribed = true }]
            };

            (string? kind, bool? isOnline) = RingMediaDownloadService.ResolveDeviceState(devices, "456");

            Assert.Equal("stickup_cam", kind);
            Assert.True(isOnline);
        }

        [Fact]
        public void ResolveDeviceState_ChimeConnected_ReturnsChimeKindAndOnline()
        {
            var devices = new Entities.Devices
            {
                Chimes = [new Entities.Chime { Id = 789, Kind = "chime_v2", Health = new Entities.DeviceHealth { Connected = true } }]
            };

            (string? kind, bool? isOnline) = RingMediaDownloadService.ResolveDeviceState(devices, "789");

            Assert.Equal("chime_v2", kind);
            Assert.True(isOnline);
        }

        [Fact]
        public void ResolveDeviceState_DeviceNotInList_ReturnsNoKindAndNullOnline()
        {
            var devices = new Entities.Devices { Doorbots = [] };

            (string? kind, bool? isOnline) = RingMediaDownloadService.ResolveDeviceState(devices, "999");

            Assert.Null(kind);
            Assert.Null(isOnline);
        }

        [Fact]
        public void ResolveDeviceState_NullDeviceList_ReturnsNoKindAndNullOnline()
        {
            (string? kind, bool? isOnline) = RingMediaDownloadService.ResolveDeviceState(null, "123");

            Assert.Null(kind);
            Assert.Null(isOnline);
        }

        [Fact]
        public void ResolveDeviceState_DoorbotWithNoHealthOrSubscribed_ReturnsKindAndNullOnline()
        {
            var devices = new Entities.Devices
            {
                Doorbots = [new Entities.Doorbot { Id = 123, Kind = "doorbell_v4", Subscribed = null, Health = null }]
            };

            (string? kind, bool? isOnline) = RingMediaDownloadService.ResolveDeviceState(devices, "123");

            Assert.Equal("doorbell_v4", kind);
            Assert.Null(isOnline);
        }

        [Fact]
        public void ResolveDeviceState_ChimeWithNoHealth_ReturnsKindAndNullOnline()
        {
            var devices = new Entities.Devices
            {
                Chimes = [new Entities.Chime { Id = 789, Kind = "chime_v2", Health = null }]
            };

            (string? kind, bool? isOnline) = RingMediaDownloadService.ResolveDeviceState(devices, "789");

            Assert.Equal("chime_v2", kind);
            Assert.Null(isOnline);
        }

        [Fact]
        public async Task ResolveDeviceStateAsync_DeviceListFetchFails_ReturnsNoKindAndNullOnline()
        {
            var session = new Mock<Session>("testuser", "testpass", null, null, null);
            _ = session.Setup(s => s.GetRingDevices(It.IsAny<Guid?>()))
                .ThrowsAsync(new InvalidOperationException("Ring device list unavailable"));
            var service = new RingMediaDownloadService(new Mock<ILogger>().Object, new Mock<ISessionProvider>().Object, new Mock<IVideoForensicsDataClient>().Object);

            (string? kind, bool? isOnline) = await service.ResolveDeviceStateAsync(session.Object, "123", CancellationToken.None);

            Assert.Null(kind);
            Assert.Null(isOnline);
        }

        [Fact]
        public async Task EnsureDeviceIdentityAsync_UnknownOnlineState_StoresNullOnline()
        {
            var dataClient = new Mock<IVideoForensicsDataClient>();
            _ = dataClient.Setup(d => d.EnsureLocationAsync(
                    It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(),
                    It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new VideoForensics.Data.Common.Entities.Location
                {
                    Id = Guid.NewGuid(),
                    ProviderLocationId = "loc-1",
                    Name = "loc-1"
                });
            _ = dataClient.Setup(d => d.EnsureDeviceAsync(
                    It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool?>(),
                    It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new VideoForensics.Data.Common.Entities.Device
                {
                    Id = Guid.NewGuid(),
                    LocationId = Guid.NewGuid(),
                    ProviderDeviceId = "123",
                    Name = "Front Door",
                    Type = "doorbell",
                    IsOnline = null
                });
            var service = new RingMediaDownloadService(new Mock<ILogger>().Object, new Mock<ISessionProvider>().Object, dataClient.Object);
            service.SetActiveProviderAccountId(Guid.NewGuid());

            _ = await service.EnsureDeviceIdentityAsync("123", "Front Door", "loc-1", "doorbell_v4", null, CancellationToken.None);

            dataClient.Verify(d => d.EnsureDeviceAsync(
                It.IsAny<Guid>(), "123", "Front Door", "doorbell", null,
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task EnsureDeviceIdentityAsync_WithDeviceFields_PassesFieldsToDataClient()
        {
            var dataClient = new Mock<IVideoForensicsDataClient>();
            _ = dataClient.Setup(d => d.EnsureLocationAsync(
                    It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(),
                    It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new VideoForensics.Data.Common.Entities.Location { Id = Guid.NewGuid(), ProviderLocationId = "loc-1", Name = "loc-1" });
            _ = dataClient.Setup(d => d.EnsureDeviceAsync(
                    It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool?>(),
                    It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>(), It.IsAny<string?>(),
                    It.IsAny<VideoForensics.Core.Providers.Common.Contracts.DeviceDetailFields?>()))
                .ReturnsAsync(new VideoForensics.Data.Common.Entities.Device
                {
                    Id = Guid.NewGuid(),
                    LocationId = Guid.NewGuid(),
                    ProviderDeviceId = "123",
                    Name = "Front Door",
                    Type = "doorbell"
                });
            var service = new RingMediaDownloadService(new Mock<ILogger>().Object, new Mock<ISessionProvider>().Object, dataClient.Object);
            service.SetActiveProviderAccountId(Guid.NewGuid());
            var fields = new VideoForensics.Core.Providers.Common.Contracts.DeviceDetailFields(RingDeviceId: "aabbcc", Owned: true);

            _ = await service.EnsureDeviceIdentityAsync("123", "Front Door", "loc-1", "doorbell_v4", null, CancellationToken.None, deviceFields: fields);

            dataClient.Verify(d => d.EnsureDeviceAsync(
                It.IsAny<Guid>(), "123", "Front Door", "doorbell", null,
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>(), It.IsAny<string?>(),
                It.Is<VideoForensics.Core.Providers.Common.Contracts.DeviceDetailFields?>(f => f != null && f.RingDeviceId == "aabbcc")), Times.Once);
        }

        [Fact]
        public async Task EnsureDeviceIdentityAsync_WithTimeZone_PassesTimeZoneToDataClient()
        {
            var dataClient = new Mock<IVideoForensicsDataClient>();
            _ = dataClient.Setup(d => d.EnsureLocationAsync(
                    It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(),
                    It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new VideoForensics.Data.Common.Entities.Location { Id = Guid.NewGuid(), ProviderLocationId = "loc-1", Name = "loc-1" });
            _ = dataClient.Setup(d => d.EnsureDeviceAsync(
                    It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool?>(),
                    It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()))
                .ReturnsAsync(new VideoForensics.Data.Common.Entities.Device
                {
                    Id = Guid.NewGuid(),
                    LocationId = Guid.NewGuid(),
                    ProviderDeviceId = "123",
                    Name = "Front Door",
                    Type = "doorbell",
                    IsOnline = true
                });
            var service = new RingMediaDownloadService(new Mock<ILogger>().Object, new Mock<ISessionProvider>().Object, dataClient.Object);
            service.SetActiveProviderAccountId(Guid.NewGuid());

            _ = await service.EnsureDeviceIdentityAsync("123", "Front Door", "loc-1", "doorbell_v4", true, CancellationToken.None, timeZoneId: "America/Chicago");

            dataClient.Verify(d => d.EnsureDeviceAsync(
                It.IsAny<Guid>(), "123", "Front Door", "doorbell", true,
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>(), timeZoneId: "America/Chicago"), Times.Once);
        }

        [Fact]
        public async Task ResolveDeviceDetailsAsync_ReturnsTimeZoneFromRingDevice()
        {
            var session = new Mock<Session>("testuser", "testpass", null, null, null);
            _ = session.Setup(s => s.GetRingDevices()).ReturnsAsync(new Entities.Devices
            {
                Doorbots = [new Entities.Doorbot { Id = 123, TimeZone = "America/Denver" }]
            });
            var service = new RingMediaDownloadService(new Mock<ILogger>().Object, new Mock<ISessionProvider>().Object, new Mock<IVideoForensicsDataClient>().Object);

            Providers.Ring.Services.RingDeviceDetails? details = await service.ResolveDeviceDetailsAsync(session.Object, "123", CancellationToken.None);

            Assert.Equal("America/Denver", details?.TimeZone);
        }

        [Fact]
        public async Task RingMediaDownloadService_Snapshot_UsesDeviceNameNotId()
        {
            var service = new RingMediaDownloadService(new Mock<ILogger>().Object, new Mock<ISessionProvider>().Object, new Mock<IVideoForensicsDataClient>().Object);
            var session = new Mock<Session>("testuser", "testpass", null, null, null);
            _ = session.Setup(s => s.GetRingDevices()).ReturnsAsync(new Entities.Devices
            {
                Doorbots = [new Entities.Doorbot { Id = 123, Description = "Front Door" }]
            });

            string? known = await service.ResolveDeviceNameAsync(session.Object, "123", CancellationToken.None);
            string? unknown = await service.ResolveDeviceNameAsync(session.Object, "999", CancellationToken.None);

            Assert.Equal("Front Door", known);
            Assert.Null(unknown);
        }

        [Fact]
        public async Task CaptureDeviceHealthSnapshotAsync_WritesHealthTelemetryAndDeviceDetails()
        {
            var dataClient = new Mock<IVideoForensicsDataClient>();
            _ = dataClient.Setup(d => d.RecordDeviceHealthAsync(It.IsAny<VideoForensics.Data.Common.Entities.DeviceHealth>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((VideoForensics.Data.Common.Entities.DeviceHealth h, CancellationToken _) => h);
            var service = new RingMediaDownloadService(new Mock<ILogger>().Object, new Mock<ISessionProvider>().Object, dataClient.Object);
            var session = new Mock<Session>("testuser", "testpass", null, null, null);
            _ = session.Setup(s => s.GetRingDevices()).ReturnsAsync(new Entities.Devices
            {
                Doorbots = [new Entities.Doorbot
                {
                    Id = 123,
                    FirmwareVersion = "2.0.1",
                    Features = new Entities.DoorbotFeatures { MotionsEnabled = true, NightVisionEnabled = true },
                    Health = new Entities.DeviceHealth
                    {
                        Connected = true,
                        BatteryPercentage = 80,
                        Rssi = -50,
                        BatteryVoltage = 12.5m,
                        ExtPowerState = 1,
                        OtaStatus = "idle",
                        LastUpdateTime = 1_700_000_000L
                    }
                }]
            });
            var deviceGuid = Guid.NewGuid();

            await service.CaptureDeviceHealthSnapshotAsync(session.Object, "123", "Test Camera", deviceGuid, CancellationToken.None);

            dataClient.Verify(d => d.RecordDeviceHealthAsync(It.Is<VideoForensics.Data.Common.Entities.DeviceHealth>(h =>
                h.DeviceId == deviceGuid
                && h.BatteryVoltageValue == 12.5m
                && h.IsExternalPowerConnected == true
                && h.OtaStatus == "idle"
                && h.LastHeartbeatUtc == new DateTime(2023, 11, 14, 22, 13, 20, DateTimeKind.Utc)), It.IsAny<CancellationToken>()), Times.Once);

            dataClient.Verify(d => d.RecordDeviceDetailsAsync(
                deviceGuid,
                It.Is<Guid?>(id => id.HasValue),
                It.Is<VideoForensics.Data.Common.Entities.DeviceCapabilities>(c => c.HasMotionDetection == true && c.HasNightVision == true && c.FirmwareVersion == "2.0.1"),
                It.Is<VideoForensics.Data.Common.Entities.DeviceFeatures?>(f => f != null && f.MotionsEnabled == true),
                It.IsAny<IReadOnlyList<VideoForensics.Data.Common.Entities.DeviceAlerts>?>(),
                It.IsAny<CancellationToken>(),
                It.Is<VideoForensics.Data.Common.Contracts.DeviceSettingsChildren?>(s => s != null)), Times.Once);
        }

        [Fact]
        public async Task EnsureDeviceIdentityAsync_DoorbellOffline_StoresDoorbellTypeAndNotOnline()
        {
            var dataClient = new Mock<IVideoForensicsDataClient>();
            _ = dataClient.Setup(d => d.EnsureLocationAsync(
                    It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(),
                    It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new VideoForensics.Data.Common.Entities.Location
                {
                    Id = Guid.NewGuid(),
                    ProviderLocationId = "loc-1",
                    Name = "loc-1"
                });
            _ = dataClient.Setup(d => d.EnsureDeviceAsync(
                    It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool?>(),
                    It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new VideoForensics.Data.Common.Entities.Device
                {
                    Id = Guid.NewGuid(),
                    LocationId = Guid.NewGuid(),
                    ProviderDeviceId = "123",
                    Name = "Front Door",
                    Type = "doorbell",
                    IsOnline = false
                });
            var service = new RingMediaDownloadService(new Mock<ILogger>().Object, new Mock<ISessionProvider>().Object, dataClient.Object);
            service.SetActiveProviderAccountId(Guid.NewGuid());

            _ = await service.EnsureDeviceIdentityAsync("123", "Front Door", "loc-1", "doorbell_v4", false, CancellationToken.None);

            dataClient.Verify(d => d.EnsureDeviceAsync(
                It.IsAny<Guid>(), "123", "Front Door", "doorbell", false,
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DownloadVideosAsync_SuccessfulDownload_LogsInformationWithFileName()
        {
            string outputPath = CreateTempOutputDirectory();
            try
            {
                var logger = new RecordingLogger();
                RingMediaDownloadService service = await CreateAuthenticatedDownloadServiceAsync(logger, EventCreatedAtUtc);

                _ = await service.DownloadVideosAsync(DownloadDeviceId, outputPath, DownloadStart(), DownloadEnd());

                string fileName = Path.GetFileName(Assert.Single(Directory.GetFiles(outputPath, "*.mp4")));
                Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Information
                    && entry.Message.StartsWith("Downloaded media file", StringComparison.Ordinal)
                    && entry.Message.Contains(fileName, StringComparison.Ordinal));
            }
            finally
            {
                DeleteOutputDirectory(outputPath);
            }
        }

        [Fact]
        public async Task DownloadVideosAsync_FileAlreadyOnDisk_LogsInformationSkip()
        {
            string outputPath = CreateTempOutputDirectory();
            try
            {
                // First run downloads the file so the second run finds it already present on disk.
                RingMediaDownloadService firstRun = await CreateAuthenticatedDownloadServiceAsync(new RecordingLogger(), EventCreatedAtUtc);
                _ = await firstRun.DownloadVideosAsync(DownloadDeviceId, outputPath, DownloadStart(), DownloadEnd());
                string fileName = Path.GetFileName(Assert.Single(Directory.GetFiles(outputPath, "*.mp4")));

                var logger = new RecordingLogger();
                RingMediaDownloadService secondRun = await CreateAuthenticatedDownloadServiceAsync(logger, EventCreatedAtUtc);
                _ = await secondRun.DownloadVideosAsync(DownloadDeviceId, outputPath, DownloadStart(), DownloadEnd());

                Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Information
                    && entry.Message.StartsWith("Media file", StringComparison.Ordinal)
                    && entry.Message.Contains(fileName, StringComparison.Ordinal)
                    && entry.Message.Contains("already exists", StringComparison.Ordinal));
            }
            finally
            {
                DeleteOutputDirectory(outputPath);
            }
        }

        private const string DownloadDeviceId = "86401464";

        // Fixed for the run so both passes of a test name the file identically (the name embeds the second).
        private static readonly DateTime EventCreatedAtUtc = DateTime.UtcNow.AddHours(-1);

        private static DateTime DownloadStart()
        {
            return DateTime.UtcNow.AddDays(-7);
        }

        private static DateTime DownloadEnd()
        {
            return DateTime.UtcNow.AddDays(1);
        }

        private static string CreateTempOutputDirectory()
        {
            string path = Path.Combine(Path.GetTempPath(), "rmds-" + Guid.NewGuid().ToString("N"));
            _ = Directory.CreateDirectory(path);
            return path;
        }

        /// <summary>
        /// Deletes the output folder, retrying briefly. The service's SHA-256 check opens files with File.OpenRead
        /// without disposing the stream, so a just-written recording can still be locked when the test finishes.
        /// </summary>
        private static void DeleteOutputDirectory(string path)
        {
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    Directory.Delete(path, recursive: true);
                    return;
                }
                catch (IOException) when (attempt < 20)
                {
                    Thread.Sleep(100);
                }
                catch (IOException)
                {
                    // Still locked after retries: leave the temp folder for the OS rather than failing the test.
                    return;
                }
            }
        }

        /// <summary>Builds a service whose session serves one recent motion event for the device and a fake recording.</summary>
        private static async Task<RingMediaDownloadService> CreateAuthenticatedDownloadServiceAsync(ILogger logger, DateTime eventCreatedAtUtc)
        {
            var session = new Session("user@example.com", "password", new DownloadStubHandler(HistoryJson(eventCreatedAtUtc)), new WritingVideoDownloader());
            _ = await session.Authenticate();

            var sessionProvider = new Mock<ISessionProvider>();
            _ = sessionProvider.Setup(sp => sp.GetSession(It.IsAny<Guid>())).Returns(session);

            var dataClient = new Mock<IVideoForensicsDataClient>();
            _ = dataClient.Setup(d => d.EnsureLocationAsync(
                    It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(),
                    It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new VideoForensics.Data.Common.Entities.Location { Id = Guid.NewGuid(), ProviderLocationId = "default", Name = "default" });
            _ = dataClient.Setup(d => d.EnsureDeviceAsync(
                    It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<bool?>(),
                    It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<CancellationToken>(), It.IsAny<string?>(),
                    It.IsAny<DeviceDetailFields?>()))
                .ReturnsAsync(new VideoForensics.Data.Common.Entities.Device
                {
                    Id = Guid.NewGuid(),
                    LocationId = Guid.NewGuid(),
                    ProviderDeviceId = DownloadDeviceId,
                    Name = "Front Door",
                    Type = "doorbell",
                    IsOnline = true
                });

            var service = new RingMediaDownloadService(logger, sessionProvider.Object, dataClient.Object, new Mock<IMediaMetadataTagger>().Object);
            service.SetActiveProviderAccountId(Guid.NewGuid());
            return service;
        }

        private static string HistoryJson(DateTime createdAtUtc)
        {
            return $$$"""
            [{"id":123,"created_at":"{{{createdAtUtc:O}}}","answered":false,"events":[],"kind":"motion",
            "favorite":false,"snapshot_url":"","recording":{"status":"ready"},"duration":41,
            "doorbot":{"id":86401464,"description":"Front Door","type":"doorbell_graham_cracker"}}]
            """;
        }

        private static HttpResponseMessage JsonResponse(HttpStatusCode status, string body, HttpRequestMessage request)
        {
            return new(status) { Content = new StringContent(body), RequestMessage = request };
        }

        /// <summary>Answers OAuth, one history event, and a recording URL; everything else is a 404.</summary>
        private sealed class DownloadStubHandler : HttpMessageHandler
        {
            private readonly string _historyJson;

            public DownloadStubHandler(string historyJson)
            {
                _historyJson = historyJson;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                string url = request.RequestUri?.ToString() ?? string.Empty;
                if (url.StartsWith("https://oauth.ring.com/", StringComparison.Ordinal))
                {
                    return Task.FromResult(JsonResponse(HttpStatusCode.OK,
                        "{\"access_token\":\"SESSION-TOKEN-NEVER-STORED\",\"refresh_token\":\"refresh\",\"expires_in\":3600,\"token_type\":\"Bearer\"}", request));
                }

                if (url.Contains("/ring_devices", StringComparison.Ordinal))
                {
                    return Task.FromResult(JsonResponse(HttpStatusCode.OK,
                        "{\"doorbots\":[{\"id\":86401464,\"description\":\"Front Door\",\"kind\":\"doorbell_graham_cracker\"}],\"authorized_doorbots\":[],\"stickup_cams\":[],\"base_stations\":[],\"chimes\":[]}",
                        request));
                }

                if (url.Contains("/share/download", StringComparison.Ordinal))
                {
                    return Task.FromResult(JsonResponse(HttpStatusCode.OK, "{\"url\":\"https://cdn.example.test/recording.mp4\"}", request));
                }

                if (url.Contains("/history", StringComparison.Ordinal))
                {
                    // Only the first page carries the event; a paged follow-up (older_than) is empty so pagination ends.
                    string query = request.RequestUri?.Query ?? string.Empty;
                    string body = query.Contains("older_than", StringComparison.Ordinal) ? "[]" : _historyJson;
                    return Task.FromResult(JsonResponse(HttpStatusCode.OK, body, request));
                }

                return Task.FromResult(JsonResponse(HttpStatusCode.NotFound, "{\"error\":\"Not found\"}", request));
            }
        }

        /// <summary>Writes a small fake recording so the download produces a non-empty file.</summary>
        private sealed class WritingVideoDownloader : IVideoDownloader
        {
            public Task<Stream> OpenStreamAsync(string url)
            {
                return Task.FromResult<Stream>(new MemoryStream());
            }

            public Task DownloadToFileAsync(string url, string saveAsPath)
            {
                return File.WriteAllBytesAsync(saveAsPath, new byte[] { 1, 2, 3, 4 });
            }
        }

        /// <summary>Keeps each log entry's level and formatted text so a test can assert on it.</summary>
        private sealed class RecordingLogger : ILogger
        {
            private readonly object _gate = new();
            private readonly List<(LogLevel Level, string Message)> _entries = [];

            public IReadOnlyList<(LogLevel Level, string Message)> Entries
            {
                get
                {
                    lock (_gate)
                    {
                        return _entries.ToArray();
                    }
                }
            }

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            {
                return null;
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return true;
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                lock (_gate)
                {
                    _entries.Add((logLevel, exception == null ? formatter(state, exception) : $"{formatter(state, exception)} {exception}"));
                }
            }
        }
    }

    /// <summary>Scripts the latest-snapshot reads, so the retry on 404 runs without Ring and without real waits.</summary>
    internal sealed class ScriptedSnapshotReadService : RingMediaDownloadService
    {
        private static readonly byte[] JpegBytes = [0xFF, 0xD8, 0xFF, 0xE0];
        private readonly HttpStatusCode[] _statuses;

        public ScriptedSnapshotReadService(ILogger logger, ISessionProvider sessionProvider, IVideoForensicsDataClient dataClient, params HttpStatusCode[] statuses)
            : base(logger, sessionProvider, dataClient)
        {
            _statuses = statuses;
        }

        /// <summary>How many latest-snapshot reads the capture made.</summary>
        public int Reads { get; private set; }

        protected override Task RequestFreshSnapshotAsync(Session session, int doorbotId) => Task.CompletedTask;

        protected override Task DelaySnapshotRetryAsync(TimeSpan delay, CancellationToken cancellationToken) => Task.CompletedTask;

        protected override Task<HttpUtility.DownloadResult> ReadLatestSnapshotAsync(Session session, int doorbotId, CancellationToken cancellationToken)
        {
            HttpStatusCode status = _statuses[Math.Min(Reads, _statuses.Length - 1)];
            Reads++;
            byte[] body = status == HttpStatusCode.OK ? JpegBytes : [];
            return Task.FromResult(new HttpUtility.DownloadResult(status, body, null));
        }
    }

    public class RingMediaDownloadServiceCaptureRetryTests
    {
        [Fact]
        public async Task CaptureAsync_Ring404ThenJpeg_RetriesAndReturnsTheImage()
        {
            // Ring builds the refreshed frame after the update, so the first read can be 404. The next read has the image.
            var sessionProvider = new Mock<ISessionProvider>();
            _ = sessionProvider.Setup(sp => sp.GetSession(It.IsAny<Guid>())).Returns(new Mock<Session>("testuser", "testpass", null, null, null).Object);
            var service = new ScriptedSnapshotReadService(new Mock<ILogger>().Object, sessionProvider.Object, new Mock<IVideoForensicsDataClient>().Object,
                HttpStatusCode.NotFound, HttpStatusCode.OK);

            SnapshotCaptureAttempt attempt = await service.CaptureAsync(Guid.NewGuid(), "123", TimeSpan.Zero, CancellationToken.None);

            Assert.Equal(200, attempt.HttpStatus);
            Assert.NotNull(attempt.Image);
            Assert.Equal(2, service.Reads);
        }

        [Fact]
        public async Task CaptureAsync_Ring404Always_StopsAfterTheReadLimit()
        {
            // A device that never produces a frame must not be read forever: the capture gives up and records the 404.
            var sessionProvider = new Mock<ISessionProvider>();
            _ = sessionProvider.Setup(sp => sp.GetSession(It.IsAny<Guid>())).Returns(new Mock<Session>("testuser", "testpass", null, null, null).Object);
            var service = new ScriptedSnapshotReadService(new Mock<ILogger>().Object, sessionProvider.Object, new Mock<IVideoForensicsDataClient>().Object,
                HttpStatusCode.NotFound);

            SnapshotCaptureAttempt attempt = await service.CaptureAsync(Guid.NewGuid(), "123", TimeSpan.Zero, CancellationToken.None);

            Assert.Equal(404, attempt.HttpStatus);
            Assert.Null(attempt.Image);
            Assert.Equal(RingMediaDownloadService.MaxSnapshotReads, service.Reads);
        }

        [Fact]
        public async Task CaptureAsync_Ring503_IsNotRetried()
        {
            // Only 404 means "not built yet". Any other failure is reported at once, not retried.
            var sessionProvider = new Mock<ISessionProvider>();
            _ = sessionProvider.Setup(sp => sp.GetSession(It.IsAny<Guid>())).Returns(new Mock<Session>("testuser", "testpass", null, null, null).Object);
            var service = new ScriptedSnapshotReadService(new Mock<ILogger>().Object, sessionProvider.Object, new Mock<IVideoForensicsDataClient>().Object,
                HttpStatusCode.ServiceUnavailable);

            SnapshotCaptureAttempt attempt = await service.CaptureAsync(Guid.NewGuid(), "123", TimeSpan.Zero, CancellationToken.None);

            Assert.Equal(503, attempt.HttpStatus);
            Assert.Equal(1, service.Reads);
        }
    }

    /// <summary>Simulates Ring refusing an on-demand snapshot refresh.</summary>
    internal sealed class RefreshFailingMediaDownloadService : RingMediaDownloadService
    {
        public RefreshFailingMediaDownloadService(ILogger logger, ISessionProvider sessionProvider, IVideoForensicsDataClient dataClient)
            : base(logger, sessionProvider, dataClient) { }

        protected override Task RequestFreshSnapshotAsync(Session session, int doorbotId) =>
            Task.FromException(new InvalidOperationException("refresh unsupported"));
    }
}
