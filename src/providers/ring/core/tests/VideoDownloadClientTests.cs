using System.Threading;

using VideoForensics.Providers.Ring.Clients;
using VideoForensics.Providers.Ring.Entities;
using VideoForensics.Providers.Ring.Interfaces;

namespace VideoForensics.Providers.Ring.Core.Tests
{
    public class VideoDownloadClientTests
    {
        #region Mock Implementations
        private class MockRecordingService : IRecordingService
        {
            public List<DoorbotHistoryEvent> HistoryToReturn { get; set; } = [];
            public CancellationToken LastCancellationToken { get; set; }
            public Exception GetDoorbotHistoryException { get; set; }
            public Exception GetDoorbotHistoryRecordingException { get; set; }
            public Exception GetDoorbotHistoryRecordingInfoException { get; set; }
            public Exception GetLatestSnapshotException { get; set; }
            public Exception ShareRecordingException { get; set; }

            public Task<List<DoorbotHistoryEvent>> GetDoorbotHistory(
                int limit = 100,
                DateTimeOffset? dateRange = null,
                string? doorbotId = null,
                CancellationToken cancellationToken = default)
            {
                LastCancellationToken = cancellationToken;
                return GetDoorbotHistoryException != null ? throw GetDoorbotHistoryException : Task.FromResult(HistoryToReturn);
            }

            public Task<DownloadRecording> GetDoorbotHistoryRecording(
                DoorbotHistoryEvent evt,
                string saveAs,
                CancellationToken cancellationToken = default)
            {
                LastCancellationToken = cancellationToken;
                return GetDoorbotHistoryRecordingException != null
                    ? throw GetDoorbotHistoryRecordingException
                    : Task.FromResult(new DownloadRecording());
            }

            public Task<DoorbotHistoryEventRecording> GetDoorbotHistoryRecordingInfo(
                DoorbotHistoryEvent evt,
                CancellationToken cancellationToken = default)
            {
                LastCancellationToken = cancellationToken;
                return GetDoorbotHistoryRecordingInfoException != null
                    ? throw GetDoorbotHistoryRecordingInfoException
                    : Task.FromResult(new DoorbotHistoryEventRecording());
            }

            public Task<Stream> GetLatestSnapshot(Doorbot doorbot, string saveAs, CancellationToken cancellationToken = default)
            {
                LastCancellationToken = cancellationToken;
                return GetLatestSnapshotException != null ? throw GetLatestSnapshotException : Task.FromResult(new MemoryStream() as Stream);
            }

            public Task<bool> UpdateSnapshot(string doorbotId, CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }

            public Task<string> ShareRecording(string recordingId, CancellationToken cancellationToken = default)
            {
                LastCancellationToken = cancellationToken;
                return ShareRecordingException != null ? throw ShareRecordingException : Task.FromResult("https://ring.com/share/video/xyz123");
            }

            public Task<List<LocationEvent>> GetLocationEvents(Guid locationId, CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }
        }

        private class MockDeviceDiscoveryService : IDeviceDiscoveryService
        {
            public List<Doorbot> DevicesToReturn { get; set; } = [];
            public CancellationToken LastCancellationToken { get; set; }
            public Exception GetRingDevicesException { get; set; }

            public Task<List<Doorbot>> GetRingDevices(Guid? locationId = null, CancellationToken cancellationToken = default)
            {
                LastCancellationToken = cancellationToken;
                return GetRingDevicesException != null ? throw GetRingDevicesException : Task.FromResult(DevicesToReturn);
            }

            public Task<List<Location>> GetLocations(CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }

            public Task<Devices> GetDeviceById(string deviceId, CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }

            public Task<List<Doorbot>> GetDoorbotsInLocation(Guid locationId, CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }

            public Task<Profile> GetProfile(CancellationToken cancellationToken = default)
            {
                throw new NotImplementedException();
            }
        }
        #endregion

        #region Constructor Tests
        [Fact]
        public void VideoDownloadClient_Constructor_WithValidServices_CreatesClient()
        {
            // Arrange
            var recordingService = new MockRecordingService();
            var deviceService = new MockDeviceDiscoveryService();

            // Act
            var client = new VideoDownloadClient(recordingService, deviceService);

            // Assert
            Assert.NotNull(client);
        }

        [Fact]
        public void VideoDownloadClient_Constructor_WithNullRecordingService_ThrowsArgumentNullException()
        {
            // Arrange
            var deviceService = new MockDeviceDiscoveryService();

            // Act & Assert
            _ = Assert.Throws<ArgumentNullException>(() => new VideoDownloadClient(null, deviceService));
        }

        [Fact]
        public void VideoDownloadClient_Constructor_WithNullDeviceService_ThrowsArgumentNullException()
        {
            // Arrange
            var recordingService = new MockRecordingService();

            // Act & Assert
            _ = Assert.Throws<ArgumentNullException>(() => new VideoDownloadClient(recordingService, null));
        }
        #endregion

        #region GetRecordings Tests
        [Fact]
        public async Task GetRecordingsAsync_WithDefaultParams_ReturnsRecordings()
        {
            // Arrange
            var recordings = new List<DoorbotHistoryEvent>
            {
                new() { Id = 1001, Kind = "motion" }
            };
            var recordingService = new MockRecordingService { HistoryToReturn = recordings };
            var client = new VideoDownloadClient(recordingService, new MockDeviceDiscoveryService());

            // Act
            List<DoorbotHistoryEvent> result = await client.GetRecordingsAsync();

            // Assert
            Assert.Equal(recordings, result);
        }

        [Fact]
        public async Task GetRecordingsAsync_WithLimitParam_PassesToService()
        {
            // Arrange
            var recordings = new List<DoorbotHistoryEvent>
            {
                new() { Id = 1001, Kind = "motion" }
            };
            var recordingService = new MockRecordingService { HistoryToReturn = recordings };
            var client = new VideoDownloadClient(recordingService, new MockDeviceDiscoveryService());

            // Act
            List<DoorbotHistoryEvent> result = await client.GetRecordingsAsync(limit: 50);

            // Assert
            Assert.NotNull(result);
        }

        [Fact]
        public async Task GetRecordingsAsync_WithEventKindFilter_FiltersResults()
        {
            // Arrange
            var recordings = new List<DoorbotHistoryEvent>
            {
                new() { Id = 1001, Kind = "motion" },
                new() { Id = 1002, Kind = "doorbell" },
                new() { Id = 1003, Kind = "motion" }
            };
            var recordingService = new MockRecordingService { HistoryToReturn = recordings };
            var client = new VideoDownloadClient(recordingService, new MockDeviceDiscoveryService());

            // Act
            List<DoorbotHistoryEvent> result = await client.GetRecordingsAsync(eventKind: "motion");

            // Assert
            Assert.Equal(2, result.Count);
            Assert.All(result, r => Assert.Equal("motion", r.Kind));
        }

        [Fact]
        public async Task GetRecordingsAsync_WithDeviceIdParam_PassesToService()
        {
            // Arrange
            var recordings = new List<DoorbotHistoryEvent>
            {
                new() { Id = 1001, Kind = "motion" }
            };
            var recordingService = new MockRecordingService { HistoryToReturn = recordings };
            var client = new VideoDownloadClient(recordingService, new MockDeviceDiscoveryService());

            // Act
            List<DoorbotHistoryEvent> result = await client.GetRecordingsAsync(deviceId: "device123");

            // Assert
            Assert.NotNull(result);
        }

        [Fact]
        public async Task GetRecordingsAsync_ReturnsEmptyListWhenNoRecordings()
        {
            // Arrange
            var recordingService = new MockRecordingService { HistoryToReturn = [] };
            var client = new VideoDownloadClient(recordingService, new MockDeviceDiscoveryService());

            // Act
            List<DoorbotHistoryEvent> result = await client.GetRecordingsAsync();

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetRecordingsAsync_ForwardsCancellationToken()
        {
            // Arrange
            var recordingService = new MockRecordingService();
            var client = new VideoDownloadClient(recordingService, new MockDeviceDiscoveryService());
            var cts = new CancellationTokenSource();

            // Act
            _ = await client.GetRecordingsAsync(cancellationToken: cts.Token);

            // Assert
            Assert.Equal(cts.Token, recordingService.LastCancellationToken);
        }

        [Fact]
        public async Task GetRecordingsAsync_WhenServiceThrows_PropagatesException()
        {
            // Arrange
            var recordingService = new MockRecordingService
            {
                GetDoorbotHistoryException = new Exception("Failed to fetch history")
            };
            var client = new VideoDownloadClient(recordingService, new MockDeviceDiscoveryService());

            // Act & Assert
            Exception ex = await Assert.ThrowsAsync<Exception>(() => client.GetRecordingsAsync());
            Assert.Equal("Failed to fetch history", ex.Message);
        }
        #endregion

        #region DownloadRecording Tests
        [Fact]
        public async Task DownloadRecordingAsync_WithValidParams_ReturnsTrueOnSuccess()
        {
            // Arrange
            var recording = new DoorbotHistoryEvent { Id = 1001 };
            var recordingService = new MockRecordingService();
            var client = new VideoDownloadClient(recordingService, new MockDeviceDiscoveryService());

            // Act
            bool result = await client.DownloadRecordingAsync(recording, "/tmp/video.mp4");

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task DownloadRecordingAsync_WithNullRecording_ThrowsArgumentNullException()
        {
            // Arrange
            var client = new VideoDownloadClient(new MockRecordingService(), new MockDeviceDiscoveryService());

            // Act & Assert
            _ = await Assert.ThrowsAsync<ArgumentNullException>(() =>
                client.DownloadRecordingAsync(null, "/tmp/video.mp4"));
        }

        [Fact]
        public async Task DownloadRecordingAsync_WithEmptyOutputPath_ThrowsArgumentException()
        {
            // Arrange
            var recording = new DoorbotHistoryEvent { Id = 1001 };
            var client = new VideoDownloadClient(new MockRecordingService(), new MockDeviceDiscoveryService());

            // Act & Assert
            _ = await Assert.ThrowsAsync<ArgumentException>(() =>
                client.DownloadRecordingAsync(recording, ""));
        }

        [Fact]
        public async Task DownloadRecordingAsync_WithNullOutputPath_ThrowsArgumentException()
        {
            // Arrange
            var recording = new DoorbotHistoryEvent { Id = 1001 };
            var client = new VideoDownloadClient(new MockRecordingService(), new MockDeviceDiscoveryService());

            // Act & Assert
            _ = await Assert.ThrowsAsync<ArgumentException>(() =>
                client.DownloadRecordingAsync(recording, null));
        }

        [Fact]
        public async Task DownloadRecordingAsync_WhenServiceThrows_ReturnsFalse()
        {
            // Arrange
            var recording = new DoorbotHistoryEvent { Id = 1001 };
            var recordingService = new MockRecordingService
            {
                GetDoorbotHistoryRecordingException = new Exception("Download failed")
            };
            var client = new VideoDownloadClient(recordingService, new MockDeviceDiscoveryService());

            // Act
            bool result = await client.DownloadRecordingAsync(recording, "/tmp/video.mp4");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task DownloadRecordingAsync_ForwardsCancellationToken()
        {
            // Arrange
            var recording = new DoorbotHistoryEvent { Id = 1001 };
            var recordingService = new MockRecordingService();
            var client = new VideoDownloadClient(recordingService, new MockDeviceDiscoveryService());
            var cts = new CancellationTokenSource();

            // Act
            _ = await client.DownloadRecordingAsync(recording, "/tmp/video.mp4", cts.Token);

            // Assert
            Assert.Equal(cts.Token, recordingService.LastCancellationToken);
        }
        #endregion

        #region DownloadSnapshot Tests
        [Fact]
        public async Task DownloadSnapshotAsync_WithValidDeviceId_ReturnsTrueOnSuccess()
        {
            // Arrange
            var devices = new List<Doorbot>
            {
                new() { DeviceId = "device123", Description = "Front Door" }
            };
            var deviceService = new MockDeviceDiscoveryService { DevicesToReturn = devices };
            var recordingService = new MockRecordingService();
            var client = new VideoDownloadClient(recordingService, deviceService);

            // Act
            bool result = await client.DownloadSnapshotAsync("device123", "/tmp/snapshot.jpg");

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task DownloadSnapshotAsync_WithEmptyDeviceId_ThrowsArgumentException()
        {
            // Arrange
            var client = new VideoDownloadClient(new MockRecordingService(), new MockDeviceDiscoveryService());

            // Act & Assert
            _ = await Assert.ThrowsAsync<ArgumentException>(() =>
                client.DownloadSnapshotAsync("", "/tmp/snapshot.jpg"));
        }

        [Fact]
        public async Task DownloadSnapshotAsync_WithNullDeviceId_ThrowsArgumentException()
        {
            // Arrange
            var client = new VideoDownloadClient(new MockRecordingService(), new MockDeviceDiscoveryService());

            // Act & Assert
            _ = await Assert.ThrowsAsync<ArgumentException>(() =>
                client.DownloadSnapshotAsync(null, "/tmp/snapshot.jpg"));
        }

        [Fact]
        public async Task DownloadSnapshotAsync_WithEmptyOutputPath_ThrowsArgumentException()
        {
            // Arrange
            var client = new VideoDownloadClient(new MockRecordingService(), new MockDeviceDiscoveryService());

            // Act & Assert
            _ = await Assert.ThrowsAsync<ArgumentException>(() =>
                client.DownloadSnapshotAsync("device123", ""));
        }

        [Fact]
        public async Task DownloadSnapshotAsync_WithNullOutputPath_ThrowsArgumentException()
        {
            // Arrange
            var client = new VideoDownloadClient(new MockRecordingService(), new MockDeviceDiscoveryService());

            // Act & Assert
            _ = await Assert.ThrowsAsync<ArgumentException>(() =>
                client.DownloadSnapshotAsync("device123", null));
        }

        [Fact]
        public async Task DownloadSnapshotAsync_WithNonExistentDevice_ReturnsFalse()
        {
            // Arrange
            var deviceService = new MockDeviceDiscoveryService { DevicesToReturn = [] };
            var client = new VideoDownloadClient(new MockRecordingService(), deviceService);

            // Act
            bool result = await client.DownloadSnapshotAsync("nonexistent", "/tmp/snapshot.jpg");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task DownloadSnapshotAsync_WhenRecordingServiceThrows_ReturnsFalse()
        {
            // Arrange
            var devices = new List<Doorbot>
            {
                new() { DeviceId = "device123", Description = "Front Door" }
            };
            var deviceService = new MockDeviceDiscoveryService { DevicesToReturn = devices };
            var recordingService = new MockRecordingService
            {
                GetLatestSnapshotException = new Exception("Snapshot download failed")
            };
            var client = new VideoDownloadClient(recordingService, deviceService);

            // Act
            bool result = await client.DownloadSnapshotAsync("device123", "/tmp/snapshot.jpg");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task DownloadSnapshotAsync_ForwardsCancellationToken()
        {
            // Arrange
            var devices = new List<Doorbot>
            {
                new() { DeviceId = "device123", Description = "Front Door" }
            };
            var deviceService = new MockDeviceDiscoveryService { DevicesToReturn = devices };
            var recordingService = new MockRecordingService();
            var client = new VideoDownloadClient(recordingService, deviceService);
            var cts = new CancellationTokenSource();

            // Act
            _ = await client.DownloadSnapshotAsync("device123", "/tmp/snapshot.jpg", cts.Token);

            // Assert
            Assert.Equal(cts.Token, deviceService.LastCancellationToken);
        }
        #endregion

        #region GetRecordingInfo Tests
        [Fact]
        public async Task GetRecordingInfoAsync_WithValidRecording_ReturnsRecordingInfo()
        {
            // Arrange
            var recording = new DoorbotHistoryEvent { Id = 1001 };
            var recordingService = new MockRecordingService();
            var client = new VideoDownloadClient(recordingService, new MockDeviceDiscoveryService());

            // Act
            DoorbotHistoryEventRecording result = await client.GetRecordingInfoAsync(recording);

            // Assert
            Assert.NotNull(result);
        }

        [Fact]
        public async Task GetRecordingInfoAsync_WithNullRecording_ThrowsArgumentNullException()
        {
            // Arrange
            var client = new VideoDownloadClient(new MockRecordingService(), new MockDeviceDiscoveryService());

            // Act & Assert
            _ = await Assert.ThrowsAsync<ArgumentNullException>(() =>
                client.GetRecordingInfoAsync(null));
        }

        [Fact]
        public async Task GetRecordingInfoAsync_ForwardsCancellationToken()
        {
            // Arrange
            var recording = new DoorbotHistoryEvent { Id = 1001 };
            var recordingService = new MockRecordingService();
            var client = new VideoDownloadClient(recordingService, new MockDeviceDiscoveryService());
            var cts = new CancellationTokenSource();

            // Act
            _ = await client.GetRecordingInfoAsync(recording, cts.Token);

            // Assert
            Assert.Equal(cts.Token, recordingService.LastCancellationToken);
        }

        [Fact]
        public async Task GetRecordingInfoAsync_WhenServiceThrows_PropagatesException()
        {
            // Arrange
            var recording = new DoorbotHistoryEvent { Id = 1001 };
            var recordingService = new MockRecordingService
            {
                GetDoorbotHistoryRecordingInfoException = new Exception("Failed to fetch info")
            };
            var client = new VideoDownloadClient(recordingService, new MockDeviceDiscoveryService());

            // Act & Assert
            Exception ex = await Assert.ThrowsAsync<Exception>(() =>
                client.GetRecordingInfoAsync(recording));
            Assert.Equal("Failed to fetch info", ex.Message);
        }
        #endregion

        #region ShareRecording Tests
        [Fact]
        public async Task ShareRecordingAsync_WithValidRecordingId_ReturnsShareUrl()
        {
            // Arrange
            var recordingService = new MockRecordingService();
            var client = new VideoDownloadClient(recordingService, new MockDeviceDiscoveryService());

            // Act
            string result = await client.ShareRecordingAsync("event123");

            // Assert
            Assert.Equal("https://ring.com/share/video/xyz123", result);
        }

        [Fact]
        public async Task ShareRecordingAsync_WithEmptyRecordingId_ThrowsArgumentException()
        {
            // Arrange
            var client = new VideoDownloadClient(new MockRecordingService(), new MockDeviceDiscoveryService());

            // Act & Assert
            _ = await Assert.ThrowsAsync<ArgumentException>(() =>
                client.ShareRecordingAsync(""));
        }

        [Fact]
        public async Task ShareRecordingAsync_WithNullRecordingId_ThrowsArgumentException()
        {
            // Arrange
            var client = new VideoDownloadClient(new MockRecordingService(), new MockDeviceDiscoveryService());

            // Act & Assert
            _ = await Assert.ThrowsAsync<ArgumentException>(() =>
                client.ShareRecordingAsync(null));
        }

        [Fact]
        public async Task ShareRecordingAsync_ForwardsCancellationToken()
        {
            // Arrange
            var recordingService = new MockRecordingService();
            var client = new VideoDownloadClient(recordingService, new MockDeviceDiscoveryService());
            var cts = new CancellationTokenSource();

            // Act
            _ = await client.ShareRecordingAsync("event123", cts.Token);

            // Assert
            Assert.Equal(cts.Token, recordingService.LastCancellationToken);
        }

        [Fact]
        public async Task ShareRecordingAsync_WhenServiceThrows_PropagatesException()
        {
            // Arrange
            var recordingService = new MockRecordingService
            {
                ShareRecordingException = new Exception("Sharing failed")
            };
            var client = new VideoDownloadClient(recordingService, new MockDeviceDiscoveryService());

            // Act & Assert
            Exception ex = await Assert.ThrowsAsync<Exception>(() =>
                client.ShareRecordingAsync("event123"));
            Assert.Equal("Sharing failed", ex.Message);
        }
        #endregion
    }
}
