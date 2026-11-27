using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.Repositories;
using VideoForensics.Utils.Testing;

using Xunit;

namespace VideoForensics.Data.Database.Tests.Repositories
{
    /// <summary>The placeholder flag on a MediaItem and the skip reason on a SnapshotCapture are stored and read back.</summary>
    public class SnapshotPlaceholderRepositoryTests : IAsyncLifetime
    {
        private static readonly DateTimeOffset T0 = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

        private SqliteInMemoryFixture _fixture = null!;
        private SnapshotCaptureRepository _captures = null!;
        private MediaItemRepository _media = null!;
        private LocationRepository _locations = null!;
        private DeviceRepository _devices = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            ILoggerFactory loggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(b => { });
            _captures = new SnapshotCaptureRepository(NewUnitOfWork(loggerFactory), loggerFactory.CreateLogger<SnapshotCaptureRepository>());
            _media = new MediaItemRepository(NewUnitOfWork(loggerFactory), NullLogger<MediaItemRepository>.Instance);
            _locations = new LocationRepository(NewUnitOfWork(loggerFactory), loggerFactory.CreateLogger<LocationRepository>());
            _devices = new DeviceRepository(NewUnitOfWork(loggerFactory), loggerFactory.CreateLogger<DeviceRepository>());
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        private UnitOfWork NewUnitOfWork(ILoggerFactory loggerFactory) =>
            new(_fixture.Factory, new TestServiceProvider(_fixture, loggerFactory), loggerFactory.CreateLogger<UnitOfWork>());

        /// <summary>Seeds a Location and Device (FK target) and returns the device's internal GUID.</summary>
        private async Task<Guid> SeedDeviceAsync()
        {
            Location location = TestDataBuilder.BuildLocation(name: "Placeholder Site");
            await _locations.AddAsync(location, CancellationToken.None);
            Device device = TestDataBuilder.BuildDevice(locationId: location.Id, name: "Placeholder Camera");
            await _devices.AddAsync(device, CancellationToken.None);
            return device.Id;
        }

        [Fact]
        public async Task MediaItem_IsPlaceholderTrue_ReadsBackTrue()
        {
            Guid deviceId = await SeedDeviceAsync();
            var item = new MediaItem
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                FileName = "gray.jpg",
                FilePath = "Devices/x/gray.jpg",
                MediaFormat = "jpeg",
                FileSizeBytes = 5442,
                RecordedAtUtc = T0.UtcDateTime,
                DownloadedAtUtc = T0.UtcDateTime,
                Sha256Hash = "2653d118053d",
                IsPlaceholder = true,
            };
            await _media.AddAsync(item, CancellationToken.None);

            MediaItem? read = await _media.GetAsync(item.Id, CancellationToken.None);

            Assert.NotNull(read);
            Assert.True(read!.IsPlaceholder);
        }

        [Fact]
        public async Task SnapshotCapture_SkipReasonIdenticalFrame_ReadsBackWithNoMediaPath()
        {
            Guid deviceId = await SeedDeviceAsync();
            var skipped = SnapshotCapture.Create(deviceId, T0, "fresh", 200, null, null, SnapshotSkipReason.IdenticalFrame);
            _ = await _captures.AddAsync(skipped, CancellationToken.None);

            IReadOnlyList<SnapshotCapture> rows = await _captures.GetAttemptsForDeviceAsync(
                deviceId, T0.AddMinutes(-1), T0.AddMinutes(1), CancellationToken.None);

            SnapshotCapture row = Assert.Single(rows);
            Assert.Equal(SnapshotSkipReason.IdenticalFrame, row.SkipReason);
            Assert.Equal(200, row.HttpStatus);
            Assert.Null(row.MediaFilePath);
        }

        /// <summary>Adds a snapshot JPEG row for a device, flagged as a placeholder or not.</summary>
        private async Task AddSnapshotAsync(Guid deviceId, string fileName, DateTime recordedAtUtc, string sha256, bool isPlaceholder)
        {
            var item = new MediaItem
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                FileName = fileName,
                FilePath = $"Devices/x/{fileName}",
                MediaFormat = "jpg",
                MediaKind = MediaKinds.Snapshot,
                RecordedAtUtc = recordedAtUtc,
                DownloadedAtUtc = recordedAtUtc,
                MetadataJson = "{\"type\":\"snapshot\"}",
                Sha256Hash = sha256,
                IsPlaceholder = isPlaceholder,
            };
            await _media.AddAsync(item, CancellationToken.None);
        }

        [Fact]
        public async Task GetSnapshotsByDeviceAndDateRangeAsync_PlaceholderSnapshot_IsExcluded()
        {
            Guid deviceId = await SeedDeviceAsync();
            await AddSnapshotAsync(deviceId, "normal.jpg", T0.UtcDateTime.AddMinutes(1), new string('a', 64), isPlaceholder: false);
            await AddSnapshotAsync(deviceId, "placeholder.jpg", T0.UtcDateTime.AddMinutes(2), new string('b', 64), isPlaceholder: true);

            IReadOnlyList<MediaItem> rows = await _media.GetSnapshotsByDeviceAndDateRangeAsync(
                deviceId, T0.UtcDateTime.AddMinutes(-5), T0.UtcDateTime.AddMinutes(5), CancellationToken.None);

            Assert.Equal(new[] { "normal.jpg" }, rows.Select(r => r.FileName).ToArray());
        }

        [Fact]
        public async Task GetLatestSnapshotHashAsync_NewerPlaceholder_ReturnsNewerNonPlaceholderHash()
        {
            Guid deviceId = await SeedDeviceAsync();
            string normalHash = new('a', 64);
            await AddSnapshotAsync(deviceId, "normal.jpg", T0.UtcDateTime.AddMinutes(1), normalHash, isPlaceholder: false);
            await AddSnapshotAsync(deviceId, "placeholder.jpg", T0.UtcDateTime.AddMinutes(9), new string('b', 64), isPlaceholder: true);

            string? hash = await _media.GetLatestSnapshotHashAsync(deviceId, CancellationToken.None);

            Assert.Equal(normalHash, hash);
        }

        [Fact]
        public async Task GetLatestSnapshotHashAsync_PlaceholderOnlyDevice_ReturnsNull()
        {
            Guid deviceId = await SeedDeviceAsync();
            await AddSnapshotAsync(deviceId, "placeholder.jpg", T0.UtcDateTime.AddMinutes(1), new string('b', 64), isPlaceholder: true);

            string? hash = await _media.GetLatestSnapshotHashAsync(deviceId, CancellationToken.None);

            Assert.Null(hash);
        }

        /// <summary>Adds a snapshot-kind (or other-kind) row and returns its Id, so a test can read the flag back.</summary>
        private async Task<Guid> AddRowAsync(Guid deviceId, string fileName, DateTime recordedAtUtc, string sha256, string mediaFormat, string mediaKind)
        {
            var item = new MediaItem
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                FileName = fileName,
                FilePath = $"Devices/x/{fileName}",
                MediaFormat = mediaFormat,
                MediaKind = mediaKind,
                RecordedAtUtc = recordedAtUtc,
                DownloadedAtUtc = recordedAtUtc,
                MetadataJson = "{\"type\":\"snapshot\"}",
                Sha256Hash = sha256,
            };
            await _media.AddAsync(item, CancellationToken.None);
            return item.Id;
        }

        private async Task<bool> IsPlaceholderAsync(Guid id)
        {
            MediaItem? row = await _media.GetAsync(id, CancellationToken.None);
            return row!.IsPlaceholder;
        }

        [Fact]
        public async Task MarkRepeatSnapshotPlaceholdersAsync_MarksOnlyRepeatOfImmediatePredecessor()
        {
            Guid deviceId = await SeedDeviceAsync();
            string a = new('a', 64);
            string b = new('b', 64);
            Guid s1 = await AddRowAsync(deviceId, "s1.jpg", T0.UtcDateTime.AddMinutes(1), a, "jpg", MediaKinds.Snapshot);
            Guid s2 = await AddRowAsync(deviceId, "s2.jpg", T0.UtcDateTime.AddMinutes(2), a, "jpg", MediaKinds.Snapshot);
            Guid s3 = await AddRowAsync(deviceId, "s3.jpg", T0.UtcDateTime.AddMinutes(3), b, "jpg", MediaKinds.Snapshot);
            Guid s4 = await AddRowAsync(deviceId, "s4.jpg", T0.UtcDateTime.AddMinutes(4), a, "jpg", MediaKinds.Snapshot);
            // A clip with hash B sits between s2 and s3. It is not a snapshot, so it must not become s3's predecessor.
            _ = await AddRowAsync(deviceId, "clip.mp4", T0.UtcDateTime.AddMinutes(2).AddSeconds(30), b, "mp4", MediaKinds.Clip);

            int marked = await _media.MarkRepeatSnapshotPlaceholdersAsync(500, CancellationToken.None);

            Assert.Equal(1, marked);
            Assert.False(await IsPlaceholderAsync(s1));
            Assert.True(await IsPlaceholderAsync(s2));
            Assert.False(await IsPlaceholderAsync(s3));
            Assert.False(await IsPlaceholderAsync(s4));
        }

        [Fact]
        public async Task MarkRepeatSnapshotPlaceholdersAsync_SecondCall_MarksNothing()
        {
            Guid deviceId = await SeedDeviceAsync();
            string a = new('a', 64);
            _ = await AddRowAsync(deviceId, "s1.jpg", T0.UtcDateTime.AddMinutes(1), a, "jpg", MediaKinds.Snapshot);
            _ = await AddRowAsync(deviceId, "s2.jpg", T0.UtcDateTime.AddMinutes(2), a, "jpg", MediaKinds.Snapshot);

            int first = await _media.MarkRepeatSnapshotPlaceholdersAsync(500, CancellationToken.None);
            int second = await _media.MarkRepeatSnapshotPlaceholdersAsync(500, CancellationToken.None);

            Assert.Equal(1, first);
            Assert.Equal(0, second);
        }

        [Fact]
        public async Task MarkRepeatSnapshotPlaceholdersAsync_SameHashOnAnotherDevice_DoesNotMarkThisDevice()
        {
            Guid deviceOne = await SeedDeviceAsync();
            Location location = TestDataBuilder.BuildLocation(name: "Second Site");
            await _locations.AddAsync(location, CancellationToken.None);
            Device other = TestDataBuilder.BuildDevice(locationId: location.Id, name: "Second Camera");
            await _devices.AddAsync(other, CancellationToken.None);

            string a = new('a', 64);
            Guid oneFirst = await AddRowAsync(deviceOne, "one.jpg", T0.UtcDateTime.AddMinutes(1), a, "jpg", MediaKinds.Snapshot);
            Guid twoFirst = await AddRowAsync(other.Id, "two-a.jpg", T0.UtcDateTime.AddMinutes(2), a, "jpg", MediaKinds.Snapshot);
            Guid twoRepeat = await AddRowAsync(other.Id, "two-b.jpg", T0.UtcDateTime.AddMinutes(3), a, "jpg", MediaKinds.Snapshot);

            int marked = await _media.MarkRepeatSnapshotPlaceholdersAsync(500, CancellationToken.None);

            Assert.Equal(1, marked);
            Assert.False(await IsPlaceholderAsync(oneFirst));
            Assert.False(await IsPlaceholderAsync(twoFirst));
            Assert.True(await IsPlaceholderAsync(twoRepeat));
        }

        [Fact]
        public async Task MarkRepeatSnapshotPlaceholdersAsync_BatchSize_LimitsRowsPerCall()
        {
            Guid deviceId = await SeedDeviceAsync();
            string a = new('a', 64);
            _ = await AddRowAsync(deviceId, "s1.jpg", T0.UtcDateTime.AddMinutes(1), a, "jpg", MediaKinds.Snapshot);
            _ = await AddRowAsync(deviceId, "s2.jpg", T0.UtcDateTime.AddMinutes(2), a, "jpg", MediaKinds.Snapshot);
            _ = await AddRowAsync(deviceId, "s3.jpg", T0.UtcDateTime.AddMinutes(3), a, "jpg", MediaKinds.Snapshot);
            _ = await AddRowAsync(deviceId, "s4.jpg", T0.UtcDateTime.AddMinutes(4), a, "jpg", MediaKinds.Snapshot);

            int first = await _media.MarkRepeatSnapshotPlaceholdersAsync(2, CancellationToken.None);
            int second = await _media.MarkRepeatSnapshotPlaceholdersAsync(2, CancellationToken.None);
            int third = await _media.MarkRepeatSnapshotPlaceholdersAsync(2, CancellationToken.None);

            Assert.Equal(2, first);
            Assert.Equal(1, second);
            Assert.Equal(0, third);
        }
    }
}
