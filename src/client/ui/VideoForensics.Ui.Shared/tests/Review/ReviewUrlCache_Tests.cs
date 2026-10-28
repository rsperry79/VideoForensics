using VideoForensics.Client.Ui.Shared.Services.Review;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Utils.Testing;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests;

/// <summary>Batching, caching, five-minute refresh, and preview URL derivation of the review URL cache.</summary>
public class ReviewUrlCache_Tests
{
    private readonly FakeMediaContentUrlProvider _provider = new();
    private readonly FakeTimeProvider _time = new(new DateTime(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc));

    private ReviewUrlCache CreateCache() => new(_provider, _time);

    private static MediaItem Item(bool withPreview) => new()
    {
        Id = Guid.NewGuid(),
        DeviceId = Guid.NewGuid(),
        FileName = "clip.mp4",
        FilePath = "clip.mp4",
        MediaFormat = "mp4",
        Sha256Hash = "hash",
        PreviewPath = withPreview ? "preview.mp4" : null,
    };

    [Fact]
    public async Task ReviewUrlCache_TwoHundredFiftyItems_RequestsInBatchesOf200()
    {
        // Arrange
        ReviewUrlCache cache = CreateCache();
        var items = Enumerable.Range(0, 250).Select(_ => Item(false)).ToList();

        // Act
        await cache.EnsureAsync(items, CancellationToken.None);

        // Assert
        Assert.Equal([200, 50], _provider.Requests.Select(batch => batch.Count));
    }

    [Fact]
    public async Task ReviewUrlCache_SecondEnsureForFreshItems_UsesTheCache()
    {
        // Arrange
        ReviewUrlCache cache = CreateCache();
        MediaItem item = Item(false);
        await cache.EnsureAsync([item], CancellationToken.None);

        // Act
        await cache.EnsureAsync([item], CancellationToken.None);

        // Assert
        Assert.Single(_provider.Requests);
        Assert.NotNull(cache.ContentUrl(item.Id));
    }

    [Fact]
    public void ReviewUrlCache_BeforeAnyEnsure_ReturnsNull()
    {
        // Arrange
        ReviewUrlCache cache = CreateCache();

        // Act and Assert
        Assert.Null(cache.ContentUrl(Guid.NewGuid()));
        Assert.Null(cache.PreviewUrl(Guid.NewGuid()));
    }

    [Fact]
    public async Task ReviewUrlCache_AfterFiveMinutes_HidesTheStaleUrlUntilEnsureRefreshesIt()
    {
        // Arrange
        ReviewUrlCache cache = CreateCache();
        MediaItem item = Item(false);
        await cache.EnsureAsync([item], CancellationToken.None);
        string firstUrl = cache.ContentUrl(item.Id)!;

        // Act and Assert: exactly five minutes is still fresh
        _time.SetUtcNow(_time.GetUtcNow().UtcDateTime + ReviewUrlCache.Lifetime);
        Assert.Equal(firstUrl, cache.ContentUrl(item.Id));

        // Past five minutes the ticket may be expired, so the cache hides it
        _time.SetUtcNow(_time.GetUtcNow().UtcDateTime + TimeSpan.FromSeconds(1));
        Assert.Null(cache.ContentUrl(item.Id));

        await cache.EnsureAsync([item], CancellationToken.None);
        Assert.Equal(2, _provider.Requests.Count);
        Assert.NotEqual(firstUrl, cache.ContentUrl(item.Id));
    }

    [Fact]
    public async Task ReviewUrlCache_PreviewUrl_IsReturnedOnlyForItemsWithAPreview()
    {
        // Arrange
        ReviewUrlCache cache = CreateCache();
        MediaItem withPreview = Item(true);
        MediaItem withoutPreview = Item(false);

        // Act
        await cache.EnsureAsync([withPreview, withoutPreview], CancellationToken.None);

        // Assert
        Assert.Equal(
            cache.ContentUrl(withPreview.Id)!.Replace("/content?", "/preview?", StringComparison.Ordinal),
            cache.PreviewUrl(withPreview.Id));
        Assert.Contains("/preview?ticket=", cache.PreviewUrl(withPreview.Id));
        Assert.NotNull(cache.ContentUrl(withoutPreview.Id));
        Assert.Null(cache.PreviewUrl(withoutPreview.Id));
    }

    [Fact]
    public async Task ReviewUrlCache_ProviderOmitsAnId_ReturnsNullForIt()
    {
        // Arrange
        ReviewUrlCache cache = CreateCache();
        MediaItem item = Item(false);
        _provider.Omitted.Add(item.Id);

        // Act
        await cache.EnsureAsync([item], CancellationToken.None);

        // Assert
        Assert.Null(cache.ContentUrl(item.Id));
    }

    [Fact]
    public async Task ReviewUrlCache_NoItems_DoesNotCallTheProvider()
    {
        // Arrange
        ReviewUrlCache cache = CreateCache();

        // Act
        await cache.EnsureAsync([], CancellationToken.None);

        // Assert
        Assert.Empty(_provider.Requests);
    }
}
