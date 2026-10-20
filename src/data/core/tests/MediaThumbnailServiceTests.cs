using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using VideoForensics.Core.Providers.Core;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Services;

using Xunit;

namespace VideoForensics.Data.Core.Tests;

public class MediaThumbnailServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "vf-thumb-" + Guid.NewGuid());
    private readonly Mock<IMediaFrameExtractor> _frames = new();

    public MediaThumbnailServiceTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private MediaThumbnailService CreateService() => new(_frames.Object, NullLogger<MediaThumbnailService>.Instance);

    private string WriteMedia(string name, string format = "video/mp4")
    {
        string path = Path.Combine(_dir, name);
        File.WriteAllBytes(path, new byte[] { 0x00, 0x01 });
        return path;
    }

    private static MediaItem MediaFor(string path, string format = "video/mp4") => new()
    {
        Id = Guid.NewGuid(),
        DeviceId = Guid.NewGuid(),
        FileName = Path.GetFileName(path),
        FilePath = path,
        MediaFormat = format,
        Sha256Hash = "hash",
    };

    private void FrameAt(long offsetMs, byte[] png) =>
        _frames.Setup(f => f.ExtractFrameAsync(It.IsAny<string>(), offsetMs, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FrameExtractionResult { Success = true, PngBytes = png });

    [Fact]
    public void ThumbnailPathFor_UsesStemAndThumbPng()
    {
        string path = Path.Combine("dir", "clip.mp4");

        Assert.Equal(Path.Combine("dir", "clip.thumb.png"), MediaThumbnailService.ThumbnailPathFor(path));
    }

    [Fact]
    public async Task EnsureThumbnailAsync_Video_WritesSidecarFromFirstSecond()
    {
        string media = WriteMedia("clip.mp4");
        FrameAt(1000, new byte[] { 0x89, 0x50, 0x4E, 0x47 });

        bool ok = await CreateService().EnsureThumbnailAsync(MediaFor(media), CancellationToken.None);

        Assert.True(ok);
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, File.ReadAllBytes(MediaThumbnailService.ThumbnailPathFor(media)));
    }

    [Fact]
    public async Task EnsureThumbnailAsync_SidecarAlreadyExists_ReturnsTrueWithoutExtracting()
    {
        string media = WriteMedia("clip.mp4");
        File.WriteAllBytes(MediaThumbnailService.ThumbnailPathFor(media), new byte[] { 0x89 });

        bool ok = await CreateService().EnsureThumbnailAsync(MediaFor(media), CancellationToken.None);

        Assert.True(ok);
        _frames.Verify(f => f.ExtractFrameAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EnsureThumbnailAsync_Image_ReturnsFalseAndDoesNotExtract()
    {
        string image = WriteMedia("still.jpg", "image/jpeg");

        bool ok = await CreateService().EnsureThumbnailAsync(MediaFor(image, "image/jpeg"), CancellationToken.None);

        Assert.False(ok);
        Assert.False(File.Exists(MediaThumbnailService.ThumbnailPathFor(image)));
        _frames.Verify(f => f.ExtractFrameAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EnsureThumbnailAsync_ExtractorFailsAtBothOffsets_ReturnsFalseAndWritesNothing()
    {
        string media = WriteMedia("clip.mp4");
        _frames.Setup(f => f.ExtractFrameAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FrameExtractionResult { Success = false, ErrorMessage = "no frame" });

        bool ok = await CreateService().EnsureThumbnailAsync(MediaFor(media), CancellationToken.None);

        Assert.False(ok);
        Assert.False(File.Exists(MediaThumbnailService.ThumbnailPathFor(media)));
    }

    [Fact]
    public async Task ResolveThumbnailPathAsync_Image_ReturnsTheImageItself()
    {
        string image = WriteMedia("still.jpg");

        string? path = await CreateService().ResolveThumbnailPathAsync(MediaFor(image, "image/jpeg"), CancellationToken.None);

        Assert.Equal(image, path);
    }

    [Fact]
    public async Task ResolveThumbnailPathAsync_Video_ReturnsSidecarAfterGeneratingIt()
    {
        string media = WriteMedia("clip.mp4");
        FrameAt(1000, new byte[] { 0x89 });

        string? path = await CreateService().ResolveThumbnailPathAsync(MediaFor(media), CancellationToken.None);

        Assert.Equal(MediaThumbnailService.ThumbnailPathFor(media), path);
        Assert.True(File.Exists(path));
    }

    [Fact]
    public async Task ResolveThumbnailPathAsync_VideoWithNoFrame_ReturnsNull()
    {
        string media = WriteMedia("clip.mp4");
        _frames.Setup(f => f.ExtractFrameAsync(It.IsAny<string>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FrameExtractionResult { Success = false, ErrorMessage = "no frame" });

        string? path = await CreateService().ResolveThumbnailPathAsync(MediaFor(media), CancellationToken.None);

        Assert.Null(path);
    }

    /// <summary>A clip shorter than one second has no frame at 1s, so the first frame (offset 0) is the fallback.</summary>
    [Fact]
    public async Task EnsureThumbnailAsync_NoFrameAtOneSecond_FallsBackToFirstFrame()
    {
        string media = WriteMedia("short.mp4");
        _frames.Setup(f => f.ExtractFrameAsync(It.IsAny<string>(), 1000, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FrameExtractionResult { Success = false, ErrorMessage = "past end" });
        FrameAt(0, new byte[] { 0x42 });

        bool ok = await CreateService().EnsureThumbnailAsync(MediaFor(media), CancellationToken.None);

        Assert.True(ok);
        Assert.Equal(new byte[] { 0x42 }, File.ReadAllBytes(MediaThumbnailService.ThumbnailPathFor(media)));
    }
}
