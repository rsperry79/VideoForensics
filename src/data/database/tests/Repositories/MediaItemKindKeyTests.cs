using Microsoft.EntityFrameworkCore;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;
using VideoForensics.Utils.Testing;

using Xunit;

namespace VideoForensics.Data.Database.Tests.Repositories
{
    /// <summary>
    /// A snapshot, a clip and a time-lapse loop can start at the same instant, and a loop starts at an event time.
    /// The unique key therefore includes the media kind, so one kind can't store two rows at the same instant.
    /// </summary>
    public class MediaItemKindKeyTests : IAsyncLifetime
    {
        private static readonly DateTime Instant = new(2026, 11, 7, 9, 0, 0, DateTimeKind.Utc);
        private SqliteInMemoryFixture _fixture = null!;
        private Guid _deviceId;

        public async ValueTask InitializeAsync()
        {
            _fixture = new SqliteInMemoryFixture();
            await _fixture.InitializeAsync();
            Device device = TestDataBuilder.BuildDevice(providerDeviceId: "86401464");
            await using VideoForensicsDbContext db = _fixture.Factory.CreateDbContext();
            _ = db.Devices.Add(device);
            _ = await db.SaveChangesAsync();
            _deviceId = device.Id;
        }

        public async ValueTask DisposeAsync()
        {
            await _fixture.DisposeAsync();
            _fixture.Dispose();
        }

        [Fact]
        public async Task MediaItems_SnapshotAndTimelapseAtTheSameInstant_BothSave()
        {
            await using VideoForensicsDbContext db = _fixture.Factory.CreateDbContext();
            _ = db.MediaItems.Add(NewItem("Snapshot", "jpg", "snap.jpg"));
            _ = db.MediaItems.Add(NewItem("Timelapse", "mp4", "loop.mp4"));

            _ = await db.SaveChangesAsync();

            Assert.Equal(2, await db.MediaItems.CountAsync(m => m.DeviceId == _deviceId && m.RecordedAtUtc == Instant));
        }

        [Fact]
        public async Task MediaItems_TwoRowsOfTheSameKindAtOneInstant_IsRejected()
        {
            await using VideoForensicsDbContext db = _fixture.Factory.CreateDbContext();
            _ = db.MediaItems.Add(NewItem("Clip", "mp4", "clip-a.mp4"));
            _ = db.MediaItems.Add(NewItem("Clip", "mp4", "clip-b.mp4"));

            _ = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }

        private MediaItem NewItem(string kind, string format, string fileName)
        {
            return new MediaItem
            {
                Id = Guid.NewGuid(),
                DeviceId = _deviceId,
                FileName = fileName,
                FilePath = fileName,
                MediaFormat = format,
                MediaKind = kind,
                RecordedAtUtc = Instant,
                DownloadedAtUtc = Instant,
                Sha256Hash = Guid.NewGuid().ToString("N"),
            };
        }
    }
}
