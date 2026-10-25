using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using VideoForensics.Data.Core.Contracts;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Services;

using Xunit;

namespace VideoForensics.Data.Core.Tests;

public class MediaPreviewBackfillTests
{
    private readonly Mock<IMediaItemRepository> _items = new();
    private readonly Mock<IMediaPreviewService> _previews = new();

    private MediaPreviewBackfill CreateSut()
    {
        return new(_items.Object, _previews.Object, NullLogger<MediaPreviewBackfill>.Instance);
    }

    private static MediaItem Video(string name)
    {
        return new()
        {
            Id = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            FileName = name,
            FilePath = Path.Combine(Path.GetTempPath(), name),
            MediaFormat = "video/mp4",
            Sha256Hash = "h",
        };
    }

    [Fact]
    public async Task RunAsync_AllItemsGenerate_GeneratedEqualsProcessed()
    {
        MediaItem first = Video("a.mp4");
        MediaItem second = Video("b.mp4");
        _ = _items.Setup(r => r.ListVideosWithoutPreviewAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([first, second]);
        _ = _previews.Setup(p => p.EnsurePreviewAsync(It.IsAny<MediaItem>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        MediaPreviewBackfillResult result = await CreateSut().RunAsync(10, CancellationToken.None);

        Assert.Equal(2, result.Processed);
        Assert.Equal(result.Processed, result.Generated);
        Assert.Equal(0, result.Failed);
    }

    [Fact]
    public async Task RunAsync_OneItemThrows_OthersStillGenerateAndFailedIsOne()
    {
        MediaItem failing = Video("a.mp4");
        MediaItem ok = Video("b.mp4");
        _ = _items.Setup(r => r.ListVideosWithoutPreviewAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([failing, ok]);
        _ = _previews.Setup(p => p.EnsurePreviewAsync(failing, It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("boom"));
        _ = _previews.Setup(p => p.EnsurePreviewAsync(ok, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        MediaPreviewBackfillResult result = await CreateSut().RunAsync(10, CancellationToken.None);

        Assert.Equal(2, result.Processed);
        Assert.Equal(1, result.Generated);
        Assert.Equal(1, result.Failed);
    }

    [Fact]
    public async Task RunAsync_NoItems_ReturnsZeros()
    {
        _ = _items.Setup(r => r.ListVideosWithoutPreviewAsync(It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);

        MediaPreviewBackfillResult result = await CreateSut().RunAsync(10, CancellationToken.None);

        Assert.Equal(0, result.Processed);
        Assert.Equal(0, result.Generated);
        Assert.Equal(0, result.Failed);
    }
}
