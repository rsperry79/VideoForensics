using Microsoft.Extensions.Logging.Abstractions;

using VideoForensics.Utils.Testing;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.Repositories;

using Xunit;

namespace VideoForensics.Data.Database.Tests
{
    /// <summary>
    /// Tests for the paged multi-device clip query used by the media viewer. Paging must run in SQL, so the
    /// total count comes from the database and is independent of the page size.
    /// </summary>
    public class MediaItemPageQueryTests : IAsyncLifetime
    {
        private SqliteInMemoryFixture _fixture = null!;
        private MediaItemRepository _repository = null!;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            _repository = new MediaItemRepository(_fixture.Factory, NullLogger<MediaItemRepository>.Instance);
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        private static readonly DateTime Start = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

        private static MediaItem Clip(Guid deviceId, int minutesAfterStart, bool purged = false)
        {
            return new()
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                FileName = $"clip-{minutesAfterStart}.mp4",
                FilePath = $"clip-{minutesAfterStart}.mp4",
                MediaFormat = "mp4",
                Sha256Hash = Guid.NewGuid().ToString("N"),
                RecordedAtUtc = Start.AddMinutes(minutesAfterStart),
                DownloadedAtUtc = DateTime.UtcNow,
                IsPurged = purged,
            };
        }

        /// <summary>Verifies that the page spans several devices, is sorted oldest first, and ignores devices not asked for.</summary>
        [Fact]
        public async Task GetPageByDevicesAndDateRangeAsync_MultipleDevices_ReturnsSortedAcrossDevicesOnly()
        {
            var cameraA = Guid.NewGuid();
            var cameraB = Guid.NewGuid();
            var other = Guid.NewGuid();
            await _repository.AddAsync(Clip(cameraB, 5), CancellationToken.None);
            await _repository.AddAsync(Clip(cameraA, 1), CancellationToken.None);
            await _repository.AddAsync(Clip(cameraA, 9), CancellationToken.None);
            await _repository.AddAsync(Clip(other, 2), CancellationToken.None);

            MediaItemPage page = await _repository.GetPageByDevicesAndDateRangeAsync(
                new[] { cameraA, cameraB }, Start, Start.AddHours(1), skip: 0, take: 10, CancellationToken.None);

            Assert.Equal(3, page.TotalCount);
            Assert.Equal(new[] { "clip-1.mp4", "clip-5.mp4", "clip-9.mp4" }, page.Items.Select(m => m.FileName).ToArray());
        }

        /// <summary>Verifies that purged clips are excluded, since they cannot be played back.</summary>
        [Fact]
        public async Task GetPageByDevicesAndDateRangeAsync_PurgedClip_IsExcluded()
        {
            var device = Guid.NewGuid();
            await _repository.AddAsync(Clip(device, 1), CancellationToken.None);
            await _repository.AddAsync(Clip(device, 2, purged: true), CancellationToken.None);

            MediaItemPage page = await _repository.GetPageByDevicesAndDateRangeAsync(
                new[] { device }, Start, Start.AddHours(1), skip: 0, take: 10, CancellationToken.None);

            Assert.Equal(1, page.TotalCount);
            Assert.Single(page.Items);
        }

        /// <summary>Verifies that the total counts every matching clip, while the page returns only the requested slice.</summary>
        [Fact]
        public async Task GetPageByDevicesAndDateRangeAsync_SkipAndTake_TotalIndependentOfPage()
        {
            var device = Guid.NewGuid();
            for (int minute = 0; minute < 7; minute++)
            {
                await _repository.AddAsync(Clip(device, minute), CancellationToken.None);
            }

            MediaItemPage page = await _repository.GetPageByDevicesAndDateRangeAsync(
                new[] { device }, Start, Start.AddHours(1), skip: 5, take: 3, CancellationToken.None);

            Assert.Equal(7, page.TotalCount);
            Assert.Equal(new[] { "clip-5.mp4", "clip-6.mp4" }, page.Items.Select(m => m.FileName).ToArray());
        }

        /// <summary>Verifies that a take outside 1..500 is rejected before any query runs.</summary>
        [Fact]
        public async Task GetPageByDevicesAndDateRangeAsync_TakeZero_ThrowsArgumentOutOfRange()
        {
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                _repository.GetPageByDevicesAndDateRangeAsync(
                    new[] { Guid.NewGuid() }, Start, Start.AddHours(1), skip: 0, take: 0, CancellationToken.None));
        }
    }
}
