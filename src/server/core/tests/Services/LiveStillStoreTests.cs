using Microsoft.Extensions.Logging;

using Moq;

using System.Security.Cryptography;

using VideoForensics.Client.Common.Contracts;
using VideoForensics.Client.Core.Services;
using VideoForensics.Core.Helpers.Platform;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Client.Core.Tests.Services
{
    public class LiveStillStoreTests
    {
        private const string MediaRoot = @"F:\Media";
        private const string Provider = "Ring";
        private const string Location = "Home";
        private const string Camera = "Front Door";

        private readonly Mock<IMediaItemWriteRepository> _media = new();
        private readonly Mock<IMediaStorageProvider> _storage = new();
        private readonly Mock<IActionLogRepository> _log = new();
        private readonly Mock<ILogger<LiveStillStore>> _logger = new();
        private readonly Guid _deviceId = Guid.NewGuid();
        private readonly DateTime _captured = new(2026, 11, 28, 14, 5, 9, 123, DateTimeKind.Utc);

        private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3, 4];

        public LiveStillStoreTests()
        {
            // No existing still at the instant unless a test says otherwise.
            _ = _media
                .Setup(m => m.GetByDeviceAndDateRangeAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<MediaItem>());
        }

        private LiveStillStore CreateStore() => new(
            _logger.Object,
            _media.Object,
            _storage.Object,
            PassThroughMediaPathResolver.Instance,
            _log.Object);

        private LiveStillRequest Request(byte[]? jpeg = null, DateTime? at = null) => new(
            _deviceId, MediaRoot, Provider, Location, Camera, at ?? _captured, jpeg ?? Jpeg, "operator-1");

        private static string Sha(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

        private MediaItem Existing(Guid id, byte[] bytes, DateTime at) => new()
        {
            Id = id,
            DeviceId = _deviceId,
            FileName = "existing.jpg",
            FilePath = "existing.jpg",
            MediaFormat = "jpg",
            MediaKind = MediaKinds.Still,
            RecordedAtUtc = at,
            DownloadedAtUtc = at,
            Sha256Hash = Sha(bytes),
            FileSizeBytes = bytes.Length
        };

        [Fact]
        public async Task StoreAsync_ValidJpeg_WritesFileAndStillRow()
        {
            LiveStillResult result = await CreateStore().StoreAsync(Request(), CancellationToken.None);

            Assert.True(result.Success);
            Assert.NotNull(result.MediaItemId);
            _media.Verify(m => m.AddAsync(
                It.Is<MediaItem>(i =>
                    i.Id == result.MediaItemId
                    && i.DeviceId == _deviceId
                    && i.MediaKind == MediaKinds.Still
                    && i.MediaFormat == "jpg"
                    && i.Sha256Hash == Sha(Jpeg)
                    && i.FileSizeBytes == Jpeg.Length
                    && i.RecordedAtUtc == _captured
                    && !i.IsPlaceholder),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task StoreAsync_ValidJpeg_SavesUnderTheDeviceStillsFolder()
        {
            string folder = Path.Combine(MediaFolderLayout.GetDeviceFolder(MediaRoot, Provider, Location, Camera), "stills");

            _ = await CreateStore().StoreAsync(Request(), CancellationToken.None);

            _storage.Verify(s => s.SaveAsync(
                It.Is<string>(p => p.StartsWith(folder, StringComparison.OrdinalIgnoreCase) && p.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)),
                It.IsAny<Stream>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task StoreAsync_ValidJpeg_WritesActionLogWithOperator()
        {
            LiveStillResult result = await CreateStore().StoreAsync(Request(), CancellationToken.None);

            _log.Verify(l => l.AppendAsync(
                "operator-1",
                ActorType.Human,
                "CaptureLiveStill",
                nameof(MediaItem),
                result.MediaItemId!.Value,
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task StoreAsync_InstantIsTruncatedToTheMillisecond()
        {
            var withTicks = new DateTime(2026, 11, 28, 14, 5, 9, 123, DateTimeKind.Utc).AddTicks(7);

            LiveStillResult result = await CreateStore().StoreAsync(Request(at: withTicks), CancellationToken.None);

            Assert.True(result.Success);
            _media.Verify(m => m.AddAsync(
                It.Is<MediaItem>(i => i.RecordedAtUtc == _captured),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task StoreAsync_NotAJpeg_ReturnsInvalidInput_WithoutWriting()
        {
            byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

            LiveStillResult result = await CreateStore().StoreAsync(Request(jpeg: png), CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(LiveStillError.InvalidInput, result.Error);
            _storage.Verify(s => s.SaveAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Never);
            _media.Verify(m => m.AddAsync(It.IsAny<MediaItem>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task StoreAsync_EmptyBody_ReturnsInvalidInput()
        {
            LiveStillResult result = await CreateStore().StoreAsync(Request(jpeg: []), CancellationToken.None);

            Assert.Equal(LiveStillError.InvalidInput, result.Error);
        }

        [Fact]
        public async Task StoreAsync_OversizedBody_ReturnsInvalidInput()
        {
            byte[] big = new byte[(20 * 1024 * 1024) + 1];
            big[0] = 0xFF; big[1] = 0xD8; big[2] = 0xFF;

            LiveStillResult result = await CreateStore().StoreAsync(Request(jpeg: big), CancellationToken.None);

            Assert.Equal(LiveStillError.InvalidInput, result.Error);
        }

        [Fact]
        public async Task StoreAsync_SameInstantSameBytes_ReturnsExistingId_WithoutWriting()
        {
            var existingId = Guid.NewGuid();
            _ = _media
                .Setup(m => m.GetByDeviceAndDateRangeAsync(_deviceId, _captured, _captured, It.IsAny<CancellationToken>()))
                .ReturnsAsync([Existing(existingId, Jpeg, _captured)]);

            LiveStillResult result = await CreateStore().StoreAsync(Request(), CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(existingId, result.MediaItemId);
            _storage.Verify(s => s.SaveAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Never);
            _media.Verify(m => m.AddAsync(It.IsAny<MediaItem>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task StoreAsync_SameInstantDifferentBytes_ReturnsConflict_WithoutWriting()
        {
            byte[] other = [0xFF, 0xD8, 0xFF, 9, 9, 9];
            _ = _media
                .Setup(m => m.GetByDeviceAndDateRangeAsync(_deviceId, _captured, _captured, It.IsAny<CancellationToken>()))
                .ReturnsAsync([Existing(Guid.NewGuid(), other, _captured)]);

            LiveStillResult result = await CreateStore().StoreAsync(Request(), CancellationToken.None);

            Assert.False(result.Success);
            Assert.Equal(LiveStillError.Conflict, result.Error);
            _media.Verify(m => m.AddAsync(It.IsAny<MediaItem>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task StoreAsync_SaveFails_ReturnsStorageFailed()
        {
            _ = _storage
                .Setup(s => s.SaveAsync(It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new IOException("disk full"));

            LiveStillResult result = await CreateStore().StoreAsync(Request(), CancellationToken.None);

            Assert.Equal(LiveStillError.StorageFailed, result.Error);
            _media.Verify(m => m.AddAsync(It.IsAny<MediaItem>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task StoreAsync_RowInsertFails_NoWinner_DeletesItsFile_ReturnsStorageFailed()
        {
            _ = _media
                .Setup(m => m.AddAsync(It.IsAny<MediaItem>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("db down"));

            LiveStillResult result = await CreateStore().StoreAsync(Request(), CancellationToken.None);

            Assert.Equal(LiveStillError.StorageFailed, result.Error);
            _storage.Verify(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task StoreAsync_RowInsertLostRace_ReturnsTheWinner_AndKeepsItsFile()
        {
            var winnerId = Guid.NewGuid();
            var lookups = 0;
            _ = _media
                .Setup(m => m.GetByDeviceAndDateRangeAsync(_deviceId, _captured, _captured, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => ++lookups == 1
                    ? new List<MediaItem>()
                    : new List<MediaItem> { Existing(winnerId, Jpeg, _captured) });
            _ = _media
                .Setup(m => m.AddAsync(It.IsAny<MediaItem>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("unique violation"));

            LiveStillResult result = await CreateStore().StoreAsync(Request(), CancellationToken.None);

            Assert.True(result.Success);
            Assert.Equal(winnerId, result.MediaItemId);
            _storage.Verify(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
