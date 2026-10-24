using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using VideoForensics.Data.Core.Contracts;
using VideoForensics.Data.Core.Services;
using VideoForensics.Client.Hosting.BackgroundServices;

namespace VideoForensics.Client.Hosting.Tests.BackgroundServices;

public class MediaThumbnailBackfillServiceTests
{
    private static (MediaThumbnailBackfillService Service, Mock<IMediaThumbnailBackfill> Backfill) CreateSut()
    {
        var backfill = new Mock<IMediaThumbnailBackfill>();
        var services = new ServiceCollection();
        _ = services.AddScoped(_ => backfill.Object);
        IServiceScopeFactory scopes = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        return (new MediaThumbnailBackfillService(scopes, NullLogger<MediaThumbnailBackfillService>.Instance), backfill);
    }

    [Fact]
    public async Task RunPassAsync_RunsBackfillOnce()
    {
        (MediaThumbnailBackfillService? service, Mock<IMediaThumbnailBackfill>? backfill) = CreateSut();
        _ = backfill.Setup(b => b.BackfillAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MediaThumbnailBackfillResult(0, 0, 0, 0));

        await service.RunPassAsync(CancellationToken.None);

        backfill.Verify(b => b.BackfillAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>A failed pass must not stop the service: the next scheduled pass is what retries the missing thumbnails.</summary>
    [Fact]
    public async Task RunPassAsync_BackfillThrows_IsLoggedAndNotRethrown()
    {
        (MediaThumbnailBackfillService? service, Mock<IMediaThumbnailBackfill>? backfill) = CreateSut();
        _ = backfill.Setup(b => b.BackfillAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("ffmpeg missing"));

        Exception? exception = await Record.ExceptionAsync(() => service.RunPassAsync(CancellationToken.None));

        Assert.Null(exception);
    }
}
