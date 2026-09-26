using Xunit;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Ui.Shared.Services.Evidence;

namespace VideoForensics.Ui.Shared.Tests.Services.Evidence;

public class EvidenceView_ParseView_Tests
{
    [Fact]
    public void ParseView_WithNull_ReturnsTimeline()
    {
        // Act
        var result = EvidenceViewModel.ParseView(null);

        // Assert
        Assert.Equal(EvidenceView.Timeline, result);
    }

    [Fact]
    public void ParseView_WithTimeline_ReturnsTimeline()
    {
        // Act
        var result = EvidenceViewModel.ParseView("timeline");

        // Assert
        Assert.Equal(EvidenceView.Timeline, result);
    }

    [Fact]
    public void ParseView_WithGrid_ReturnsGrid()
    {
        // Act
        var result = EvidenceViewModel.ParseView("grid");

        // Assert
        Assert.Equal(EvidenceView.Grid, result);
    }

    [Fact]
    public void ParseView_WithGallery_ReturnsGallery()
    {
        // Act
        var result = EvidenceViewModel.ParseView("gallery");

        // Assert
        Assert.Equal(EvidenceView.Gallery, result);
    }

    [Fact]
    public void ParseView_CaseInsensitive()
    {
        // Act
        var result1 = EvidenceViewModel.ParseView("TIMELINE");
        var result2 = EvidenceViewModel.ParseView("Grid");
        var result3 = EvidenceViewModel.ParseView("GALLERY");

        // Assert
        Assert.Equal(EvidenceView.Timeline, result1);
        Assert.Equal(EvidenceView.Grid, result2);
        Assert.Equal(EvidenceView.Gallery, result3);
    }

    [Fact]
    public void ParseView_WithInvalid_ReturnsTimeline()
    {
        // Act
        var result = EvidenceViewModel.ParseView("invalid");

        // Assert
        Assert.Equal(EvidenceView.Timeline, result);
    }
}

public class EvidenceViewModel_Constructor_Tests
{
    [Fact]
    public void Constructor_InitializesWithEmptyResult()
    {
        // Act
        var vm = new EvidenceViewModel(new EvidenceLoadResult
        {
            Items = [],
            Errors = new Dictionary<Guid, string>(),
            IntegrityRecords = [],
            ActiveHoldsByMediaItemId = new Dictionary<Guid, LegalHold>()
        });

        // Assert
        Assert.NotNull(vm.Items);
        Assert.Empty(vm.Items);
        Assert.Null(vm.Selected);
        Assert.False(vm.IsViewerOpen);
    }
}

public class EvidenceViewModel_Open_Tests
{
    [Fact]
    public void Open_SelectsItemByKey()
    {
        // Arrange
        var item1 = new EvidenceItem
        {
            Key = "event:1",
            Kind = EvidenceKind.Event,
            OccurredAtUtc = DateTime.UtcNow,
            DeviceId = Guid.NewGuid(),
            DeviceName = "Device1",
            Event = new Event { Id = Guid.NewGuid(), EventType = "Test", ProviderEventId = "test:1", MetadataJson = "{}" }
        };
        var item2 = new EvidenceItem
        {
            Key = "event:2",
            Kind = EvidenceKind.Event,
            OccurredAtUtc = DateTime.UtcNow,
            DeviceId = Guid.NewGuid(),
            DeviceName = "Device2",
            Event = new Event { Id = Guid.NewGuid(), EventType = "Test2", ProviderEventId = "test:2", MetadataJson = "{}" }
        };
        var result = new EvidenceLoadResult
        {
            Items = new[] { item1, item2 },
            Errors = new Dictionary<Guid, string>(),
            IntegrityRecords = new List<IntegrityRecord>(),
            ActiveHoldsByMediaItemId = new Dictionary<Guid, LegalHold>()
        };
        var vm = new EvidenceViewModel(result);

        // Act
        vm.Open("event:2");

        // Assert
        Assert.Equal(item2, vm.Selected);
    }

    [Fact]
    public void Open_WithUnknownKey_IsNoOp()
    {
        // Arrange
        var item = new EvidenceItem
        {
            Key = "event:1",
            Kind = EvidenceKind.Event,
            OccurredAtUtc = DateTime.UtcNow,
            DeviceId = Guid.NewGuid(),
            DeviceName = "Device1",
            Event = new Event { Id = Guid.NewGuid(), EventType = "Test", ProviderEventId = "test:1", MetadataJson = "{}" }
        };
        var result = new EvidenceLoadResult
        {
            Items = new[] { item },
            Errors = new Dictionary<Guid, string>(),
            IntegrityRecords = new List<IntegrityRecord>(),
            ActiveHoldsByMediaItemId = new Dictionary<Guid, LegalHold>()
        };
        var vm = new EvidenceViewModel(result);

        // Act
        vm.Open("unknown:999");

        // Assert
        Assert.Null(vm.Selected);
    }
}

public class EvidenceViewModel_Close_Tests
{
    [Fact]
    public void Close_ClearsSelectedAndHidesViewer()
    {
        // Arrange
        var item = new EvidenceItem
        {
            Key = "event:1",
            Kind = EvidenceKind.Event,
            OccurredAtUtc = DateTime.UtcNow,
            DeviceId = Guid.NewGuid(),
            DeviceName = "Device1",
            Event = new Event { Id = Guid.NewGuid(), EventType = "Test", ProviderEventId = "test:1", MetadataJson = "{}" }
        };
        var result = new EvidenceLoadResult
        {
            Items = new[] { item },
            Errors = new Dictionary<Guid, string>(),
            IntegrityRecords = new List<IntegrityRecord>(),
            ActiveHoldsByMediaItemId = new Dictionary<Guid, LegalHold>()
        };
        var vm = new EvidenceViewModel(result);
        vm.Open("event:1");

        // Act
        vm.Close();

        // Assert
        Assert.Null(vm.Selected);
        Assert.False(vm.IsViewerOpen);
    }
}

public class EvidenceViewModel_Navigation_Tests
{
    [Fact]
    public void MoveNext_SelectsNextViewableItem()
    {
        // Arrange
        var item1 = new EvidenceItem
        {
            Key = "media:1",
            Kind = EvidenceKind.Snapshot,
            OccurredAtUtc = DateTime.UtcNow.AddHours(-1),
            DeviceId = Guid.NewGuid(),
            DeviceName = "Device1",
            Media = new MediaItem { Id = Guid.NewGuid(), FileName = "a.jpg", FilePath = "/", MediaFormat = "image/jpeg", Sha256Hash = "a", FileSizeBytes = 100, RecordedAtUtc = DateTime.UtcNow.AddHours(-1), DownloadedAtUtc = DateTime.UtcNow }
        };
        var item2 = new EvidenceItem
        {
            Key = "media:2",
            Kind = EvidenceKind.Video,
            OccurredAtUtc = DateTime.UtcNow,
            DeviceId = Guid.NewGuid(),
            DeviceName = "Device2",
            Media = new MediaItem { Id = Guid.NewGuid(), FileName = "b.mp4", FilePath = "/", MediaFormat = "video/mp4", Sha256Hash = "b", FileSizeBytes = 1000, RecordedAtUtc = DateTime.UtcNow, DownloadedAtUtc = DateTime.UtcNow }
        };
        var result = new EvidenceLoadResult
        {
            Items = new[] { item2, item1 },  // Descending order
            Errors = new Dictionary<Guid, string>(),
            IntegrityRecords = new List<IntegrityRecord>(),
            ActiveHoldsByMediaItemId = new Dictionary<Guid, LegalHold>()
        };
        var vm = new EvidenceViewModel(result);
        vm.Open("media:2");

        // Act
        vm.MoveNext();

        // Assert
        Assert.Equal(item1, vm.Selected);
    }

    [Fact]
    public void MovePrevious_SelectsPreviousViewableItem()
    {
        // Arrange
        var item1 = new EvidenceItem
        {
            Key = "media:1",
            Kind = EvidenceKind.Snapshot,
            OccurredAtUtc = DateTime.UtcNow.AddHours(-1),
            DeviceId = Guid.NewGuid(),
            DeviceName = "Device1",
            Media = new MediaItem { Id = Guid.NewGuid(), FileName = "a.jpg", FilePath = "/", MediaFormat = "image/jpeg", Sha256Hash = "a", FileSizeBytes = 100, RecordedAtUtc = DateTime.UtcNow.AddHours(-1), DownloadedAtUtc = DateTime.UtcNow }
        };
        var item2 = new EvidenceItem
        {
            Key = "media:2",
            Kind = EvidenceKind.Video,
            OccurredAtUtc = DateTime.UtcNow,
            DeviceId = Guid.NewGuid(),
            DeviceName = "Device2",
            Media = new MediaItem { Id = Guid.NewGuid(), FileName = "b.mp4", FilePath = "/", MediaFormat = "video/mp4", Sha256Hash = "b", FileSizeBytes = 1000, RecordedAtUtc = DateTime.UtcNow, DownloadedAtUtc = DateTime.UtcNow }
        };
        var result = new EvidenceLoadResult
        {
            Items = new[] { item2, item1 },  // Descending order
            Errors = new Dictionary<Guid, string>(),
            IntegrityRecords = new List<IntegrityRecord>(),
            ActiveHoldsByMediaItemId = new Dictionary<Guid, LegalHold>()
        };
        var vm = new EvidenceViewModel(result);
        vm.Open("media:1");

        // Act
        vm.MovePrevious();

        // Assert
        Assert.Equal(item2, vm.Selected);
    }

    [Fact]
    public void HasNext_TrueWhenNextExists()
    {
        // Arrange
        var item1 = new EvidenceItem
        {
            Key = "media:1",
            Kind = EvidenceKind.Snapshot,
            OccurredAtUtc = DateTime.UtcNow.AddHours(-1),
            DeviceId = Guid.NewGuid(),
            DeviceName = "Device1",
            Media = new MediaItem { Id = Guid.NewGuid(), FileName = "a.jpg", FilePath = "/", MediaFormat = "image/jpeg", Sha256Hash = "a", FileSizeBytes = 100, RecordedAtUtc = DateTime.UtcNow.AddHours(-1), DownloadedAtUtc = DateTime.UtcNow }
        };
        var item2 = new EvidenceItem
        {
            Key = "media:2",
            Kind = EvidenceKind.Video,
            OccurredAtUtc = DateTime.UtcNow,
            DeviceId = Guid.NewGuid(),
            DeviceName = "Device2",
            Media = new MediaItem { Id = Guid.NewGuid(), FileName = "b.mp4", FilePath = "/", MediaFormat = "video/mp4", Sha256Hash = "b", FileSizeBytes = 1000, RecordedAtUtc = DateTime.UtcNow, DownloadedAtUtc = DateTime.UtcNow }
        };
        var result = new EvidenceLoadResult
        {
            Items = new[] { item2, item1 },
            Errors = new Dictionary<Guid, string>(),
            IntegrityRecords = new List<IntegrityRecord>(),
            ActiveHoldsByMediaItemId = new Dictionary<Guid, LegalHold>()
        };
        var vm = new EvidenceViewModel(result);
        vm.Open("media:2");

        // Act
        var hasNext = vm.HasNext;

        // Assert
        Assert.True(hasNext);
    }

    [Fact]
    public void HasNext_FalseWhenNoNext()
    {
        // Arrange
        var item = new EvidenceItem
        {
            Key = "media:1",
            Kind = EvidenceKind.Snapshot,
            OccurredAtUtc = DateTime.UtcNow,
            DeviceId = Guid.NewGuid(),
            DeviceName = "Device1",
            Media = new MediaItem { Id = Guid.NewGuid(), FileName = "a.jpg", FilePath = "/", MediaFormat = "image/jpeg", Sha256Hash = "a", FileSizeBytes = 100, RecordedAtUtc = DateTime.UtcNow, DownloadedAtUtc = DateTime.UtcNow }
        };
        var result = new EvidenceLoadResult
        {
            Items = new[] { item },
            Errors = new Dictionary<Guid, string>(),
            IntegrityRecords = new List<IntegrityRecord>(),
            ActiveHoldsByMediaItemId = new Dictionary<Guid, LegalHold>()
        };
        var vm = new EvidenceViewModel(result);
        vm.Open("media:1");

        // Act
        var hasNext = vm.HasNext;

        // Assert
        Assert.False(hasNext);
    }

    [Fact]
    public void HasPrevious_TrueWhenPreviousExists()
    {
        // Arrange
        var item1 = new EvidenceItem
        {
            Key = "media:1",
            Kind = EvidenceKind.Snapshot,
            OccurredAtUtc = DateTime.UtcNow.AddHours(-1),
            DeviceId = Guid.NewGuid(),
            DeviceName = "Device1",
            Media = new MediaItem { Id = Guid.NewGuid(), FileName = "a.jpg", FilePath = "/", MediaFormat = "image/jpeg", Sha256Hash = "a", FileSizeBytes = 100, RecordedAtUtc = DateTime.UtcNow.AddHours(-1), DownloadedAtUtc = DateTime.UtcNow }
        };
        var item2 = new EvidenceItem
        {
            Key = "media:2",
            Kind = EvidenceKind.Video,
            OccurredAtUtc = DateTime.UtcNow,
            DeviceId = Guid.NewGuid(),
            DeviceName = "Device2",
            Media = new MediaItem { Id = Guid.NewGuid(), FileName = "b.mp4", FilePath = "/", MediaFormat = "video/mp4", Sha256Hash = "b", FileSizeBytes = 1000, RecordedAtUtc = DateTime.UtcNow, DownloadedAtUtc = DateTime.UtcNow }
        };
        var result = new EvidenceLoadResult
        {
            Items = new[] { item2, item1 },
            Errors = new Dictionary<Guid, string>(),
            IntegrityRecords = new List<IntegrityRecord>(),
            ActiveHoldsByMediaItemId = new Dictionary<Guid, LegalHold>()
        };
        var vm = new EvidenceViewModel(result);
        vm.Open("media:1");

        // Act
        var hasPrevious = vm.HasPrevious;

        // Assert
        Assert.True(hasPrevious);
    }

    [Fact]
    public void HasPrevious_FalseWhenNoPrevious()
    {
        // Arrange
        var item = new EvidenceItem
        {
            Key = "media:1",
            Kind = EvidenceKind.Snapshot,
            OccurredAtUtc = DateTime.UtcNow,
            DeviceId = Guid.NewGuid(),
            DeviceName = "Device1",
            Media = new MediaItem { Id = Guid.NewGuid(), FileName = "a.jpg", FilePath = "/", MediaFormat = "image/jpeg", Sha256Hash = "a", FileSizeBytes = 100, RecordedAtUtc = DateTime.UtcNow, DownloadedAtUtc = DateTime.UtcNow }
        };
        var result = new EvidenceLoadResult
        {
            Items = new[] { item },
            Errors = new Dictionary<Guid, string>(),
            IntegrityRecords = new List<IntegrityRecord>(),
            ActiveHoldsByMediaItemId = new Dictionary<Guid, LegalHold>()
        };
        var vm = new EvidenceViewModel(result);
        vm.Open("media:1");

        // Act
        var hasPrevious = vm.HasPrevious;

        // Assert
        Assert.False(hasPrevious);
    }
}

public class EvidenceViewModel_GalleryItems_Tests
{
    [Fact]
    public void GalleryItems_ReturnsOnlyItemsWithViewableMedia()
    {
        // Arrange
        var viewableItem = new EvidenceItem
        {
            Key = "media:1",
            Kind = EvidenceKind.Snapshot,
            OccurredAtUtc = DateTime.UtcNow,
            DeviceId = Guid.NewGuid(),
            DeviceName = "Device1",
            Media = new MediaItem { Id = Guid.NewGuid(), FileName = "a.jpg", FilePath = "/", MediaFormat = "image/jpeg", Sha256Hash = "a", FileSizeBytes = 100, RecordedAtUtc = DateTime.UtcNow, DownloadedAtUtc = DateTime.UtcNow }
        };
        var nonViewableItem = new EvidenceItem
        {
            Key = "event:1",
            Kind = EvidenceKind.Event,
            OccurredAtUtc = DateTime.UtcNow,
            DeviceId = Guid.NewGuid(),
            DeviceName = "Device2",
            Event = new Event { Id = Guid.NewGuid(), EventType = "Test", ProviderEventId = "test:1", MetadataJson = "{}" },
            Media = null
        };
        var result = new EvidenceLoadResult
        {
            Items = new[] { viewableItem, nonViewableItem },
            Errors = new Dictionary<Guid, string>(),
            IntegrityRecords = new List<IntegrityRecord>(),
            ActiveHoldsByMediaItemId = new Dictionary<Guid, LegalHold>()
        };
        var vm = new EvidenceViewModel(result);

        // Act
        var galleryItems = vm.GalleryItems;

        // Assert
        Assert.Single(galleryItems);
        Assert.Equal(viewableItem, galleryItems[0]);
    }
}

public class EvidenceViewModel_ThumbnailMediaIds_Tests
{
    [Fact]
    public void ThumbnailMediaIds_ReturnsImageMediaIdsCapped()
    {
        // Arrange
        var imageId1 = Guid.NewGuid();
        var imageId2 = Guid.NewGuid();
        var videoId = Guid.NewGuid();
        var item1 = new EvidenceItem
        {
            Key = "media:1",
            Kind = EvidenceKind.Snapshot,
            OccurredAtUtc = DateTime.UtcNow,
            DeviceId = Guid.NewGuid(),
            DeviceName = "Device1",
            Media = new MediaItem { Id = imageId1, FileName = "a.jpg", FilePath = "/", MediaFormat = "image/jpeg", Sha256Hash = "a", FileSizeBytes = 100, RecordedAtUtc = DateTime.UtcNow, DownloadedAtUtc = DateTime.UtcNow }
        };
        var item2 = new EvidenceItem
        {
            Key = "media:2",
            Kind = EvidenceKind.Snapshot,
            OccurredAtUtc = DateTime.UtcNow,
            DeviceId = Guid.NewGuid(),
            DeviceName = "Device2",
            Media = new MediaItem { Id = imageId2, FileName = "b.jpg", FilePath = "/", MediaFormat = "image/jpeg", Sha256Hash = "b", FileSizeBytes = 100, RecordedAtUtc = DateTime.UtcNow, DownloadedAtUtc = DateTime.UtcNow }
        };
        var item3 = new EvidenceItem
        {
            Key = "media:3",
            Kind = EvidenceKind.Video,
            OccurredAtUtc = DateTime.UtcNow,
            DeviceId = Guid.NewGuid(),
            DeviceName = "Device3",
            Media = new MediaItem { Id = videoId, FileName = "c.mp4", FilePath = "/", MediaFormat = "video/mp4", Sha256Hash = "c", FileSizeBytes = 1000, RecordedAtUtc = DateTime.UtcNow, DownloadedAtUtc = DateTime.UtcNow }
        };
        var result = new EvidenceLoadResult
        {
            Items = new[] { item1, item2, item3 },
            Errors = new Dictionary<Guid, string>(),
            IntegrityRecords = new List<IntegrityRecord>(),
            ActiveHoldsByMediaItemId = new Dictionary<Guid, LegalHold>()
        };
        var vm = new EvidenceViewModel(result);

        // Act
        var ids = vm.ThumbnailMediaIds(max: 2);

        // Assert
        Assert.Equal(2, ids.Count);
        Assert.Contains(imageId1, ids);
        Assert.Contains(imageId2, ids);
        Assert.DoesNotContain(videoId, ids);
    }

    [Fact]
    public void ThumbnailMediaIds_RespectsCap()
    {
        // Arrange
        var imageIds = Enumerable.Range(0, 100)
            .Select(i => new EvidenceItem
            {
                Key = $"media:{i}",
                Kind = EvidenceKind.Snapshot,
                OccurredAtUtc = DateTime.UtcNow.AddHours(-i),
                DeviceId = Guid.NewGuid(),
                DeviceName = "Device",
                Media = new MediaItem { Id = Guid.NewGuid(), FileName = $"{i}.jpg", FilePath = "/", MediaFormat = "image/jpeg", Sha256Hash = $"h{i}", FileSizeBytes = 100, RecordedAtUtc = DateTime.UtcNow.AddHours(-i), DownloadedAtUtc = DateTime.UtcNow }
            }).ToList();
        var result = new EvidenceLoadResult
        {
            Items = imageIds,
            Errors = new Dictionary<Guid, string>(),
            IntegrityRecords = new List<IntegrityRecord>(),
            ActiveHoldsByMediaItemId = new Dictionary<Guid, LegalHold>()
        };
        var vm = new EvidenceViewModel(result);

        // Act
        var ids = vm.ThumbnailMediaIds(max: 50);

        // Assert
        Assert.Equal(50, ids.Count);
    }
}
