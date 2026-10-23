using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Services;

using Xunit;

namespace VideoForensics.Data.Core.Tests;

public class MediaThumbnailBackfillTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vf-backfill-" + Guid.NewGuid());
    private readonly Mock<IMediaItemRepository> _items = new();
    private readonly Mock<IMediaThumbnailService> _thumbnails = new();

    public MediaThumbnailBackfillTests()
    {
        _ = Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private MediaThumbnailBackfill CreateSut()
    {
        return new(_items.Object, _thumbnails.Object, NullLogger<MediaThumbnailBackfill>.Instance);
    }

    private string OnDisk(string name)
    {
        string path = Path.Combine(_dir, name);
        File.WriteAllBytes(path, new byte[] { 0x00 });
        return path;
    }

    private static MediaItem Item(string path, string format, bool purged = false)
    {
        return new()
        {
            Id = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            FileName = Path.GetFileName(path),
            FilePath = path,
            MediaFormat = format,
            Sha256Hash = "h",
            IsPurged = purged,
        };
    }

    [Fact]
    public async Task BackfillAsync_VideosWithFiles_EnsuresEachAndCountsResults()
    {
        MediaItem ok = Item(OnDisk("a.mp4"), "video/mp4");
        MediaItem failing = Item(OnDisk("b.mp4"), "video/mp4");
        _ = _items.Setup(r => r.ListVideosWithoutThumbnailAsync(It.IsAny<CancellationToken>())).ReturnsAsync([ok, failing]);
        _ = _thumbnails.Setup(t => t.EnsureThumbnailAsync(ok, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _ = _thumbnails.Setup(t => t.EnsureThumbnailAsync(failing, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        MediaThumbnailBackfillResult result = await CreateSut().BackfillAsync(CancellationToken.None);

        Assert.Equal(2, result.Checked);
        Assert.Equal(1, result.Available);
        Assert.Equal(1, result.Unavailable);
    }

    /// <summary>A selected row whose clip is gone is counted, not attempted: there is nothing to extract a frame from.</summary>
    [Fact]
    public async Task BackfillAsync_RowWhoseFileIsGone_CountedAsMissingAndNotAttempted()
    {
        MediaItem missingFile = Item(Path.Combine(_dir, "never-written.mp4"), "video/mp4");
        _ = _items.Setup(r => r.ListVideosWithoutThumbnailAsync(It.IsAny<CancellationToken>())).ReturnsAsync([missingFile]);

        MediaThumbnailBackfillResult result = await CreateSut().BackfillAsync(CancellationToken.None);

        Assert.Equal(0, result.Checked);
        Assert.Equal(1, result.MissingFile);
        _thumbnails.Verify(t => t.EnsureThumbnailAsync(It.IsAny<MediaItem>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task BackfillAsync_NoMediaItems_ReturnsEmptyResult()
    {
        _ = _items.Setup(r => r.ListVideosWithoutThumbnailAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        MediaThumbnailBackfillResult result = await CreateSut().BackfillAsync(CancellationToken.None);

        Assert.Equal(0, result.Checked);
        Assert.Equal(0, result.Available);
        Assert.Equal(0, result.Unavailable);
    }
}
