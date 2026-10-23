using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.Repositories;

using Xunit;

namespace VideoForensics.Data.Database.Tests.Repositories
{
    public class MediaStillRepositoryTests : RepositoryTestBase
    {
        private MediaStillRepository _repository = null!;

        public override async ValueTask InitializeAsync()
        {
            await base.InitializeAsync();
            _repository = new MediaStillRepository(Fixture.Factory, CreateLogger<MediaStillRepository>());
        }

        private static MediaItem NewMediaItem(Guid? id = null, string sha = "sha-source")
        {
            return new MediaItem
            {
                Id = id ?? Guid.NewGuid(),
                DeviceId = Guid.NewGuid(),
                FileName = "video.mp4",
                FilePath = "/media/video.mp4",
                MediaFormat = "video/mp4",
                FileSizeBytes = 1024,
                Sha256Hash = sha,
                RecordedAtUtc = DateTime.UtcNow,
                DownloadedAtUtc = DateTime.UtcNow
            };
        }

        [Fact]
        public async Task CreateAsync_PersistsBothMediaItemAndCaptureRow()
        {
            var source = NewMediaItem();
            await using (var db = await Fixture.Factory.CreateDbContextAsync(CancellationToken.None))
            {
                db.MediaItems.Add(source);
                await db.SaveChangesAsync(CancellationToken.None);
            }

            var stillId = Guid.NewGuid();
            var still = NewMediaItem(stillId, sha: "sha-still");
            still.MediaFormat = "image/png";

            var capture = new MediaStillCapture
            {
                MediaItemId = stillId,
                SourceMediaItemId = source.Id,
                FrameOffsetMs = 4200,
                SourceSha256AtCapture = "sha-source",
                CapturedByOperator = "operator-1",
                CaptureMethod = MediaStillCaptureMethod.Server,
                CreatedAtUtc = DateTime.UtcNow
            };

            MediaStillCapture result = await _repository.CreateAsync(still, capture, CancellationToken.None);

            Assert.Equal(stillId, result.MediaItemId);

            await using var verifyDb = await Fixture.Factory.CreateDbContextAsync(CancellationToken.None);
            MediaItem? persistedItem = await verifyDb.MediaItems.FindAsync(new object[] { stillId }, CancellationToken.None);
            Assert.NotNull(persistedItem);
            Assert.Equal("sha-still", persistedItem!.Sha256Hash);

            MediaStillCapture? persistedCapture = await verifyDb.MediaStillCaptures.FindAsync(new object[] { stillId }, CancellationToken.None);
            Assert.NotNull(persistedCapture);
            Assert.Equal(source.Id, persistedCapture!.SourceMediaItemId);
            Assert.Equal(4200, persistedCapture.FrameOffsetMs);
            Assert.Equal(MediaStillCaptureMethod.Server, persistedCapture.CaptureMethod);
        }

        [Fact]
        public async Task GetAsync_ReturnsNull_WhenNotAStill()
        {
            MediaStillCapture? result = await _repository.GetAsync(Guid.NewGuid(), CancellationToken.None);
            Assert.Null(result);
        }

        [Fact]
        public async Task GetAsync_ReturnsCapture_WhenExists()
        {
            var source = NewMediaItem();
            var stillId = Guid.NewGuid();
            var still = NewMediaItem(stillId, sha: "sha-still");

            await using (var db = await Fixture.Factory.CreateDbContextAsync(CancellationToken.None))
            {
                db.MediaItems.Add(source);
                await db.SaveChangesAsync(CancellationToken.None);
            }

            var capture = new MediaStillCapture
            {
                MediaItemId = stillId,
                SourceMediaItemId = source.Id,
                FrameOffsetMs = 100,
                SourceSha256AtCapture = "sha-source",
                CapturedByOperator = "operator-1",
                CaptureMethod = MediaStillCaptureMethod.Client,
                CreatedAtUtc = DateTime.UtcNow
            };
            await _repository.CreateAsync(still, capture, CancellationToken.None);

            MediaStillCapture? result = await _repository.GetAsync(stillId, CancellationToken.None);
            Assert.NotNull(result);
            Assert.Equal(MediaStillCaptureMethod.Client, result!.CaptureMethod);
        }

        [Fact]
        public async Task GetBySourceMediaItemIdAsync_ReturnsAllDerivedStills()
        {
            var source = NewMediaItem();
            await using (var db = await Fixture.Factory.CreateDbContextAsync(CancellationToken.None))
            {
                db.MediaItems.Add(source);
                await db.SaveChangesAsync(CancellationToken.None);
            }

            for (int i = 0; i < 2; i++)
            {
                var stillId = Guid.NewGuid();
                var still = NewMediaItem(stillId, sha: $"sha-still-{i}");
                var capture = new MediaStillCapture
                {
                    MediaItemId = stillId,
                    SourceMediaItemId = source.Id,
                    FrameOffsetMs = i * 1000,
                    SourceSha256AtCapture = "sha-source",
                    CapturedByOperator = "operator-1",
                    CaptureMethod = MediaStillCaptureMethod.Server,
                    CreatedAtUtc = DateTime.UtcNow
                };
                await _repository.CreateAsync(still, capture, CancellationToken.None);
            }

            IReadOnlyList<MediaStillCapture> results = await _repository.GetBySourceMediaItemIdAsync(source.Id, CancellationToken.None);
            Assert.Equal(2, results.Count);
        }
    }
}
