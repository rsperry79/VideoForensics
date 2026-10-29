using Moq;

using VideoForensics.Client.Ui.Shared.Contracts;
using VideoForensics.Client.Ui.Shared.Services.Review;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>Paging, the safety cap, and device chunking of the review clip loader.</summary>
public class ReviewClipLoader_Tests
{
    private static readonly DateTime FromUtc = new(2026, 10, 27, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime ToUtc = new(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc);

    private readonly List<(int DeviceCount, int Skip, int Take)> _pageCalls = [];

    private static MediaItem Item(Guid device, int index) => new()
    {
        Id = Guid.NewGuid(),
        DeviceId = device,
        FileName = "clip-" + index + ".mp4",
        FilePath = "clip.mp4",
        MediaFormat = "mp4",
        RecordedAtUtc = FromUtc.AddSeconds(index),
        DurationSeconds = 60,
        Sha256Hash = "hash",
    };

    /// <summary>A repository double that answers pages from <paramref name="items"/>, filtered by device as the SQL query does.</summary>
    private Mock<IMediaItemRepository> RepositoryServing(IReadOnlyList<MediaItem> items)
    {
        var repository = new Mock<IMediaItemRepository>();
        _ = repository
            .Setup(r => r.GetPageByDevicesAndDateRangeAsync(
                It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns((IReadOnlyCollection<Guid> devices, DateTime from, DateTime to, int skip, int take, CancellationToken ct) =>
            {
                _pageCalls.Add((devices.Count, skip, take));
                List<MediaItem> matching = items.Where(item => devices.Contains(item.DeviceId)).ToList();
                IReadOnlyList<MediaItem> page = matching.Skip(skip).Take(take).ToList();
                return Task.FromResult(new MediaItemPage(page, matching.Count));
            });
        return repository;
    }

    [Fact]
    public async Task ReviewClipLoader_TwoPages_ReadsEveryPageAndReturnsEveryItem()
    {
        // Arrange
        Guid device = Guid.NewGuid();
        List<MediaItem> items = Enumerable.Range(0, 600).Select(i => Item(device, i)).ToList();
        var loader = new ReviewClipLoader(RepositoryServing(items).Object);

        // Act
        ReviewClipSet result = await loader.LoadAsync([device], FromUtc, ToUtc, CancellationToken.None);

        // Assert
        Assert.Equal(600, result.Items.Count);
        Assert.False(result.Truncated);
        Assert.Equal([(1, 0, 500), (1, 500, 500)], _pageCalls);
    }

    [Fact]
    public async Task ReviewClipLoader_MoreItemsThanTheSafetyCap_StopsAtTheCapAndSetsTruncated()
    {
        // Arrange
        Guid device = Guid.NewGuid();
        List<MediaItem> items = Enumerable.Range(0, ReviewClipLoader.MaxItems + 1).Select(i => Item(device, i)).ToList();
        var loader = new ReviewClipLoader(RepositoryServing(items).Object);

        // Act
        ReviewClipSet result = await loader.LoadAsync([device], FromUtc, ToUtc, CancellationToken.None);

        // Assert
        Assert.Equal(ReviewClipLoader.MaxItems, result.Items.Count);
        Assert.True(result.Truncated);
    }

    [Fact]
    public async Task ReviewClipLoader_ExactlyTheSafetyCap_IsNotTruncated()
    {
        // Arrange
        Guid device = Guid.NewGuid();
        List<MediaItem> items = Enumerable.Range(0, ReviewClipLoader.MaxItems).Select(i => Item(device, i)).ToList();
        var loader = new ReviewClipLoader(RepositoryServing(items).Object);

        // Act
        ReviewClipSet result = await loader.LoadAsync([device], FromUtc, ToUtc, CancellationToken.None);

        // Assert
        Assert.Equal(ReviewClipLoader.MaxItems, result.Items.Count);
        Assert.False(result.Truncated);
    }

    [Fact]
    public async Task ReviewClipLoader_MoreThan64Devices_SplitsTheQueryIntoChunks()
    {
        // Arrange
        var devices = Enumerable.Range(0, 70).Select(_ => Guid.NewGuid()).ToList();
        var loader = new ReviewClipLoader(RepositoryServing([]).Object);

        // Act
        ReviewClipSet result = await loader.LoadAsync(devices, FromUtc, ToUtc, CancellationToken.None);

        // Assert
        Assert.Empty(result.Items);
        Assert.Equal([64, 6], _pageCalls.Select(call => call.DeviceCount));
    }

    [Fact]
    public async Task ReviewClipLoader_NoDevices_ReturnsEmptyWithoutQuerying()
    {
        // Arrange
        var repository = RepositoryServing([]);
        var loader = new ReviewClipLoader(repository.Object);

        // Act
        ReviewClipSet result = await loader.LoadAsync([], FromUtc, ToUtc, CancellationToken.None);

        // Assert
        Assert.Empty(result.Items);
        Assert.False(result.Truncated);
        repository.Verify(r => r.GetPageByDevicesAndDateRangeAsync(
            It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
