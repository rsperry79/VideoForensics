namespace VideoForensics.Ui.Shared.Tests.Services.Evidence;

using Xunit;
using Moq;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Contracts;
using VideoForensics.Data.Core.Models;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Ui.Shared.Services.Events;
using VideoForensics.Ui.Shared.Services.Scope;
using VideoForensics.Ui.Shared.Services.Evidence;

public class EvidenceLoader_LoadAsync_EventItems_Tests
{
    [Fact]
    public async Task LoadAsync_WithEvents_CreatesEventEvidenceItems()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var device = new Device
        {
            Id = deviceId,
            LocationId = Guid.NewGuid(),
            Name = "Front Door",
            ProviderDeviceId = "provider-id",
            Type = "camera",
            IsOnline = true,
            MetadataJson = "{}"
        };

        var scope = new ForensicScope(
            DeviceIds: new[] { deviceId },
            FromUtc: new DateTime(2026, 9, 17, 0, 0, 0, DateTimeKind.Utc),
            ToUtc: new DateTime(2026, 9, 24, 0, 0, 0, DateTimeKind.Utc),
            Search: null);

        var testEvent = new Event
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            EventType = "Motion",
            OccurredAtUtc = new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc),
            DiscoveredAtUtc = new DateTime(2026, 9, 20, 12, 5, 0, DateTimeKind.Utc),
            DownloadedAtUtc = new DateTime(2026, 9, 20, 12, 10, 0, DateTimeKind.Utc),
            ProviderEventId = "provider-event-1",
            MetadataJson = "{}"
        };

        var mockEventRepo = new Mock<IEventRepository>();
        mockEventRepo
            .Setup(r => r.ListByDeviceAndDateRangeAsync(deviceId, scope.FromUtc, scope.ToUtc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { testEvent });

        var mockReportService = new Mock<IReportGenerationService>();
        mockReportService
            .Setup(r => r.BuildEvidenceReviewAsync(deviceId, scope.FromUtc, scope.ToUtc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EvidenceReviewReport { MediaItems = new List<MediaItem>(), IntegrityRecords = new List<IntegrityRecord>() });

        var mockHoldRepo = new Mock<ILegalHoldRepository>();

        mockHoldRepo
            .Setup(r => r.GetActiveByMediaItemIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LegalHold>());
        mockHoldRepo
            .Setup(r => r.GetActiveByMediaItemIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LegalHold>());

        var mockMediaRepo = new Mock<IMediaItemRepository>();
        mockMediaRepo
            .Setup(r => r.GetByDeviceAndDateRangeAsync(deviceId, scope.FromUtc, scope.ToUtc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MediaItem>());

        var eventRowLoader = new EventRowLoader(mockEventRepo.Object, mockReportService.Object, mockHoldRepo.Object);
        var loader = new EvidenceLoader(eventRowLoader, mockMediaRepo.Object);

        // Act
        var result = await loader.LoadAsync(scope, new[] { device }, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Items);
        var item = result.Items[0];
        Assert.Equal(EvidenceKind.Event, item.Kind);
        Assert.Equal("Motion", item.Label);
        Assert.Equal(deviceId, item.DeviceId);
        Assert.Equal("Front Door", item.DeviceName);
        Assert.NotNull(item.Event);
        Assert.Equal(testEvent.Id, item.Event.Id);
    }
}

public class EvidenceLoader_LoadAsync_MediaItems_Tests
{
    [Fact]
    public async Task LoadAsync_WithMediaNotAttachedToEvent_CreatesMediaEvidenceItems()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var device = new Device
        {
            Id = deviceId,
            LocationId = Guid.NewGuid(),
            Name = "Front Door",
            ProviderDeviceId = "provider-id",
            Type = "camera",
            IsOnline = true,
            MetadataJson = "{}"
        };

        var scope = new ForensicScope(
            DeviceIds: new[] { deviceId },
            FromUtc: new DateTime(2026, 9, 17, 0, 0, 0, DateTimeKind.Utc),
            ToUtc: new DateTime(2026, 9, 24, 0, 0, 0, DateTimeKind.Utc),
            Search: null);

        var mediaItem = new MediaItem
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            FileName = "snapshot.jpg",
            FilePath = "/path/snapshot.jpg",
            MediaFormat = "image/jpeg",
            Sha256Hash = "hash",
            RecordedAtUtc = new DateTime(2026, 9, 20, 12, 30, 0, DateTimeKind.Utc),
            DownloadedAtUtc = new DateTime(2026, 9, 20, 12, 35, 0, DateTimeKind.Utc),
            IsPurged = false
        };

        var mockEventRepo = new Mock<IEventRepository>();
        mockEventRepo
            .Setup(r => r.ListByDeviceAndDateRangeAsync(deviceId, scope.FromUtc, scope.ToUtc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Event>());

        var mockReportService = new Mock<IReportGenerationService>();
        mockReportService
            .Setup(r => r.BuildEvidenceReviewAsync(deviceId, scope.FromUtc, scope.ToUtc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EvidenceReviewReport { MediaItems = new List<MediaItem>(), IntegrityRecords = new List<IntegrityRecord>() });

        var mockHoldRepo = new Mock<ILegalHoldRepository>();

        mockHoldRepo
            .Setup(r => r.GetActiveByMediaItemIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LegalHold>());
        mockHoldRepo
            .Setup(r => r.GetActiveByMediaItemIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LegalHold>());

        var mockMediaRepo = new Mock<IMediaItemRepository>();
        mockMediaRepo
            .Setup(r => r.GetByDeviceAndDateRangeAsync(deviceId, scope.FromUtc, scope.ToUtc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { mediaItem });

        var eventRowLoader = new EventRowLoader(mockEventRepo.Object, mockReportService.Object, mockHoldRepo.Object);
        var loader = new EvidenceLoader(eventRowLoader, mockMediaRepo.Object);

        // Act
        var result = await loader.LoadAsync(scope, new[] { device }, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Items);
        var item = result.Items[0];
        Assert.Equal(EvidenceKind.Snapshot, item.Kind);
        Assert.Equal("Snapshot", item.Label);
        Assert.Equal(deviceId, item.DeviceId);
        Assert.NotNull(item.Media);
        Assert.Equal(mediaItem.Id, item.Media.Id);
    }

    [Fact]
    public async Task LoadAsync_WithPurgedMedia_SkipsPurgedMedia()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var device = new Device
        {
            Id = deviceId,
            LocationId = Guid.NewGuid(),
            Name = "Front Door",
            ProviderDeviceId = "provider-id",
            Type = "camera",
            IsOnline = true,
            MetadataJson = "{}"
        };

        var scope = new ForensicScope(
            DeviceIds: new[] { deviceId },
            FromUtc: new DateTime(2026, 9, 17, 0, 0, 0, DateTimeKind.Utc),
            ToUtc: new DateTime(2026, 9, 24, 0, 0, 0, DateTimeKind.Utc),
            Search: null);

        var purgedMedia = new MediaItem
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            FileName = "deleted.jpg",
            FilePath = "/path/deleted.jpg",
            MediaFormat = "image/jpeg",
            Sha256Hash = "hash",
            RecordedAtUtc = new DateTime(2026, 9, 20, 12, 30, 0, DateTimeKind.Utc),
            DownloadedAtUtc = new DateTime(2026, 9, 20, 12, 35, 0, DateTimeKind.Utc),
            IsPurged = true,
            PurgedAtUtc = DateTime.UtcNow,
            PurgeReason = "Retention policy"
        };

        var mockEventRepo = new Mock<IEventRepository>();
        mockEventRepo
            .Setup(r => r.ListByDeviceAndDateRangeAsync(deviceId, scope.FromUtc, scope.ToUtc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Event>());

        var mockReportService = new Mock<IReportGenerationService>();
        mockReportService
            .Setup(r => r.BuildEvidenceReviewAsync(deviceId, scope.FromUtc, scope.ToUtc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EvidenceReviewReport { MediaItems = new List<MediaItem>(), IntegrityRecords = new List<IntegrityRecord>() });

        var mockHoldRepo = new Mock<ILegalHoldRepository>();

        mockHoldRepo
            .Setup(r => r.GetActiveByMediaItemIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LegalHold>());
        mockHoldRepo
            .Setup(r => r.GetActiveByMediaItemIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LegalHold>());
        var mockMediaRepo = new Mock<IMediaItemRepository>();
        mockMediaRepo
            .Setup(r => r.GetByDeviceAndDateRangeAsync(deviceId, scope.FromUtc, scope.ToUtc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { purgedMedia });

        var eventRowLoader = new EventRowLoader(mockEventRepo.Object, mockReportService.Object, mockHoldRepo.Object);
        var loader = new EvidenceLoader(eventRowLoader, mockMediaRepo.Object);

        // Act
        var result = await loader.LoadAsync(scope, new[] { device }, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task LoadAsync_WithVideoMedia_ClassifiesAsVideo()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var device = new Device
        {
            Id = deviceId,
            LocationId = Guid.NewGuid(),
            Name = "Front Door",
            ProviderDeviceId = "provider-id",
            Type = "camera",
            IsOnline = true,
            MetadataJson = "{}"
        };

        var scope = new ForensicScope(
            DeviceIds: new[] { deviceId },
            FromUtc: new DateTime(2026, 9, 17, 0, 0, 0, DateTimeKind.Utc),
            ToUtc: new DateTime(2026, 9, 24, 0, 0, 0, DateTimeKind.Utc),
            Search: null);

        var videoMedia = new MediaItem
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            FileName = "video.mp4",
            FilePath = "/path/video.mp4",
            MediaFormat = "video/mp4",
            Sha256Hash = "hash",
            RecordedAtUtc = new DateTime(2026, 9, 20, 12, 30, 0, DateTimeKind.Utc),
            DownloadedAtUtc = new DateTime(2026, 9, 20, 12, 35, 0, DateTimeKind.Utc),
            IsPurged = false
        };

        var mockEventRepo = new Mock<IEventRepository>();
        mockEventRepo
            .Setup(r => r.ListByDeviceAndDateRangeAsync(deviceId, scope.FromUtc, scope.ToUtc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Event>());

        var mockReportService = new Mock<IReportGenerationService>();
        mockReportService
            .Setup(r => r.BuildEvidenceReviewAsync(deviceId, scope.FromUtc, scope.ToUtc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EvidenceReviewReport { MediaItems = new List<MediaItem>(), IntegrityRecords = new List<IntegrityRecord>() });

        var mockHoldRepo = new Mock<ILegalHoldRepository>();

        mockHoldRepo
            .Setup(r => r.GetActiveByMediaItemIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LegalHold>());
        var mockMediaRepo = new Mock<IMediaItemRepository>();
        mockMediaRepo
            .Setup(r => r.GetByDeviceAndDateRangeAsync(deviceId, scope.FromUtc, scope.ToUtc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { videoMedia });

        var eventRowLoader = new EventRowLoader(mockEventRepo.Object, mockReportService.Object, mockHoldRepo.Object);
        var loader = new EvidenceLoader(eventRowLoader, mockMediaRepo.Object);

        // Act
        var result = await loader.LoadAsync(scope, new[] { device }, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Items);
        var item = result.Items[0];
        Assert.Equal(EvidenceKind.Video, item.Kind);
        Assert.Equal("Video", item.Label);
    }
}

public class EvidenceLoader_LoadAsync_Deduplication_Tests
{
    [Fact]
    public async Task LoadAsync_WithMediaAttachedToEvent_DoesNotDuplicate()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var device = new Device
        {
            Id = deviceId,
            LocationId = Guid.NewGuid(),
            Name = "Front Door",
            ProviderDeviceId = "provider-id",
            Type = "camera",
            IsOnline = true,
            MetadataJson = "{}"
        };

        var scope = new ForensicScope(
            DeviceIds: new[] { deviceId },
            FromUtc: new DateTime(2026, 9, 17, 0, 0, 0, DateTimeKind.Utc),
            ToUtc: new DateTime(2026, 9, 24, 0, 0, 0, DateTimeKind.Utc),
            Search: null);

        var mediaId = Guid.NewGuid();
        var mediaItem = new MediaItem
        {
            Id = mediaId,
            DeviceId = deviceId,
            DownloadEventId = Guid.NewGuid(),
            FileName = "video.mp4",
            FilePath = "/path/video.mp4",
            MediaFormat = "video/mp4",
            Sha256Hash = "hash",
            RecordedAtUtc = new DateTime(2026, 9, 20, 12, 30, 0, DateTimeKind.Utc),
            DownloadedAtUtc = new DateTime(2026, 9, 20, 12, 35, 0, DateTimeKind.Utc),
            IsPurged = false
        };

        var testEvent = new Event
        {
            Id = mediaItem.DownloadEventId!.Value,
            DeviceId = deviceId,
            EventType = "Motion",
            OccurredAtUtc = new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc),
            DiscoveredAtUtc = new DateTime(2026, 9, 20, 12, 5, 0, DateTimeKind.Utc),
            ProviderEventId = "provider-event-1",
            MetadataJson = "{}"
        };

        var mockEventRepo = new Mock<IEventRepository>();
        mockEventRepo
            .Setup(r => r.ListByDeviceAndDateRangeAsync(deviceId, scope.FromUtc, scope.ToUtc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { testEvent });

        var mockReportService = new Mock<IReportGenerationService>();
        mockReportService
            .Setup(r => r.BuildEvidenceReviewAsync(deviceId, scope.FromUtc, scope.ToUtc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EvidenceReviewReport { MediaItems = new List<MediaItem> { mediaItem }, IntegrityRecords = new List<IntegrityRecord>() });

        var mockHoldRepo = new Mock<ILegalHoldRepository>();

        mockHoldRepo
            .Setup(r => r.GetActiveByMediaItemIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LegalHold>());
        var mockMediaRepo = new Mock<IMediaItemRepository>();
        mockMediaRepo
            .Setup(r => r.GetByDeviceAndDateRangeAsync(deviceId, scope.FromUtc, scope.ToUtc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { mediaItem });

        var eventRowLoader = new EventRowLoader(mockEventRepo.Object, mockReportService.Object, mockHoldRepo.Object);
        var loader = new EvidenceLoader(eventRowLoader, mockMediaRepo.Object);

        // Act
        var result = await loader.LoadAsync(scope, new[] { device }, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        // Should have one item (event), not two
        Assert.Single(result.Items);
        var item = result.Items[0];
        Assert.Equal(EvidenceKind.Event, item.Kind);
        Assert.NotNull(item.Media);
        Assert.Equal(mediaId, item.Media.Id);
    }
}

public class EvidenceLoader_LoadAsync_AllDevices_Tests
{
    [Fact]
    public async Task LoadAsync_WithAllDevicesScope_QueriesAllDevices()
    {
        // Arrange
        var device1Id = Guid.NewGuid();
        var device2Id = Guid.NewGuid();
        var device1 = new Device
        {
            Id = device1Id,
            LocationId = Guid.NewGuid(),
            Name = "Front Door",
            ProviderDeviceId = "provider-id-1",
            Type = "camera",
            IsOnline = true,
            MetadataJson = "{}"
        };
        var device2 = new Device
        {
            Id = device2Id,
            LocationId = Guid.NewGuid(),
            Name = "Back Yard",
            ProviderDeviceId = "provider-id-2",
            Type = "camera",
            IsOnline = true,
            MetadataJson = "{}"
        };

        var scope = new ForensicScope(
            DeviceIds: new List<Guid>(), // Empty = all devices
            FromUtc: new DateTime(2026, 9, 17, 0, 0, 0, DateTimeKind.Utc),
            ToUtc: new DateTime(2026, 9, 24, 0, 0, 0, DateTimeKind.Utc),
            Search: null);

        var event1 = new Event
        {
            Id = Guid.NewGuid(),
            DeviceId = device1Id,
            EventType = "Motion",
            OccurredAtUtc = new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc),
            DiscoveredAtUtc = new DateTime(2026, 9, 20, 12, 5, 0, DateTimeKind.Utc),
            ProviderEventId = "event-1",
            MetadataJson = "{}"
        };

        var event2 = new Event
        {
            Id = Guid.NewGuid(),
            DeviceId = device2Id,
            EventType = "Person",
            OccurredAtUtc = new DateTime(2026, 9, 20, 13, 0, 0, DateTimeKind.Utc),
            DiscoveredAtUtc = new DateTime(2026, 9, 20, 13, 5, 0, DateTimeKind.Utc),
            ProviderEventId = "event-2",
            MetadataJson = "{}"
        };

        var mockEventRepo = new Mock<IEventRepository>();
        mockEventRepo
            .Setup(r => r.ListByDeviceAndDateRangeAsync(device1Id, scope.FromUtc, scope.ToUtc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { event1 });
        mockEventRepo
            .Setup(r => r.ListByDeviceAndDateRangeAsync(device2Id, scope.FromUtc, scope.ToUtc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { event2 });

        var mockReportService = new Mock<IReportGenerationService>();
        mockReportService
            .Setup(r => r.BuildEvidenceReviewAsync(It.IsAny<Guid>(), scope.FromUtc, scope.ToUtc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EvidenceReviewReport { MediaItems = new List<MediaItem>(), IntegrityRecords = new List<IntegrityRecord>() });

        var mockHoldRepo = new Mock<ILegalHoldRepository>();

        mockHoldRepo
            .Setup(r => r.GetActiveByMediaItemIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LegalHold>());
        var mockMediaRepo = new Mock<IMediaItemRepository>();
        mockMediaRepo
            .Setup(r => r.GetByDeviceAndDateRangeAsync(It.IsAny<Guid>(), scope.FromUtc, scope.ToUtc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MediaItem>());

        var eventRowLoader = new EventRowLoader(mockEventRepo.Object, mockReportService.Object, mockHoldRepo.Object);
        var loader = new EvidenceLoader(eventRowLoader, mockMediaRepo.Object);

        // Act
        var result = await loader.LoadAsync(scope, new[] { device1, device2 }, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Items.Count);
        mockEventRepo.Verify(
            r => r.ListByDeviceAndDateRangeAsync(device1Id, scope.FromUtc, scope.ToUtc, It.IsAny<CancellationToken>()),
            Times.Once);
        mockEventRepo.Verify(
            r => r.ListByDeviceAndDateRangeAsync(device2Id, scope.FromUtc, scope.ToUtc, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}

public class EvidenceLoader_LoadAsync_Search_Tests
{
    [Fact]
    public async Task LoadAsync_WithSearch_FiltersMediaOnlyItems()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var device = new Device
        {
            Id = deviceId,
            LocationId = Guid.NewGuid(),
            Name = "Front Door",
            ProviderDeviceId = "provider-id",
            Type = "camera",
            IsOnline = true,
            MetadataJson = "{}"
        };

        var scope = new ForensicScope(
            DeviceIds: new[] { deviceId },
            FromUtc: new DateTime(2026, 9, 17, 0, 0, 0, DateTimeKind.Utc),
            ToUtc: new DateTime(2026, 9, 24, 0, 0, 0, DateTimeKind.Utc),
            Search: "important");

        var matchingMedia = new MediaItem
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            FileName = "important_snapshot.jpg",
            FilePath = "/path/important_snapshot.jpg",
            MediaFormat = "image/jpeg",
            Sha256Hash = "hash1",
            RecordedAtUtc = new DateTime(2026, 9, 20, 12, 30, 0, DateTimeKind.Utc),
            DownloadedAtUtc = new DateTime(2026, 9, 20, 12, 35, 0, DateTimeKind.Utc),
            IsPurged = false
        };

        var nonMatchingMedia = new MediaItem
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            FileName = "other.jpg",
            FilePath = "/path/other.jpg",
            MediaFormat = "image/jpeg",
            Sha256Hash = "hash2",
            RecordedAtUtc = new DateTime(2026, 9, 20, 12, 40, 0, DateTimeKind.Utc),
            DownloadedAtUtc = new DateTime(2026, 9, 20, 12, 45, 0, DateTimeKind.Utc),
            IsPurged = false
        };

        var mockEventRepo = new Mock<IEventRepository>();
        mockEventRepo
            .Setup(r => r.ListByDeviceAndDateRangeAsync(deviceId, scope.FromUtc, scope.ToUtc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Event>());

        var mockReportService = new Mock<IReportGenerationService>();
        mockReportService
            .Setup(r => r.BuildEvidenceReviewAsync(deviceId, scope.FromUtc, scope.ToUtc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EvidenceReviewReport { MediaItems = new List<MediaItem>(), IntegrityRecords = new List<IntegrityRecord>() });

        var mockHoldRepo = new Mock<ILegalHoldRepository>();

        mockHoldRepo
            .Setup(r => r.GetActiveByMediaItemIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LegalHold>());
        var mockMediaRepo = new Mock<IMediaItemRepository>();
        mockMediaRepo
            .Setup(r => r.GetByDeviceAndDateRangeAsync(deviceId, scope.FromUtc, scope.ToUtc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { matchingMedia, nonMatchingMedia });

        var eventRowLoader = new EventRowLoader(mockEventRepo.Object, mockReportService.Object, mockHoldRepo.Object);
        var loader = new EvidenceLoader(eventRowLoader, mockMediaRepo.Object);

        // Act
        var result = await loader.LoadAsync(scope, new[] { device }, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Items);
        var item = result.Items[0];
        Assert.Equal(matchingMedia.Id, item.Media!.Id);
    }
}

public class EvidenceLoader_LoadAsync_DeviceErrors_Tests
{
    [Fact]
    public async Task LoadAsync_WithDeviceError_RecordsErrorAndContinues()
    {
        // Arrange
        var device1Id = Guid.NewGuid();
        var device2Id = Guid.NewGuid();
        var device1 = new Device
        {
            Id = device1Id,
            LocationId = Guid.NewGuid(),
            Name = "Front Door",
            ProviderDeviceId = "provider-id-1",
            Type = "camera",
            IsOnline = true,
            MetadataJson = "{}"
        };
        var device2 = new Device
        {
            Id = device2Id,
            LocationId = Guid.NewGuid(),
            Name = "Back Yard",
            ProviderDeviceId = "provider-id-2",
            Type = "camera",
            IsOnline = true,
            MetadataJson = "{}"
        };

        var scope = new ForensicScope(
            DeviceIds: new[] { device1Id, device2Id },
            FromUtc: new DateTime(2026, 9, 17, 0, 0, 0, DateTimeKind.Utc),
            ToUtc: new DateTime(2026, 9, 24, 0, 0, 0, DateTimeKind.Utc),
            Search: null);

        var event2 = new Event
        {
            Id = Guid.NewGuid(),
            DeviceId = device2Id,
            EventType = "Person",
            OccurredAtUtc = new DateTime(2026, 9, 20, 13, 0, 0, DateTimeKind.Utc),
            DiscoveredAtUtc = new DateTime(2026, 9, 20, 13, 5, 0, DateTimeKind.Utc),
            ProviderEventId = "event-2",
            MetadataJson = "{}"
        };

        var mockEventRepo = new Mock<IEventRepository>();
        mockEventRepo
            .Setup(r => r.ListByDeviceAndDateRangeAsync(device1Id, scope.FromUtc, scope.ToUtc, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Network error"));
        mockEventRepo
            .Setup(r => r.ListByDeviceAndDateRangeAsync(device2Id, scope.FromUtc, scope.ToUtc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { event2 });

        var mockReportService = new Mock<IReportGenerationService>();
        mockReportService
            .Setup(r => r.BuildEvidenceReviewAsync(It.IsAny<Guid>(), scope.FromUtc, scope.ToUtc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EvidenceReviewReport { MediaItems = new List<MediaItem>(), IntegrityRecords = new List<IntegrityRecord>() });

        var mockHoldRepo = new Mock<ILegalHoldRepository>();

        mockHoldRepo
            .Setup(r => r.GetActiveByMediaItemIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LegalHold>());
        var mockMediaRepo = new Mock<IMediaItemRepository>();
        mockMediaRepo
            .Setup(r => r.GetByDeviceAndDateRangeAsync(It.IsAny<Guid>(), scope.FromUtc, scope.ToUtc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MediaItem>());

        var eventRowLoader = new EventRowLoader(mockEventRepo.Object, mockReportService.Object, mockHoldRepo.Object);
        var loader = new EvidenceLoader(eventRowLoader, mockMediaRepo.Object);

        // Act
        var result = await loader.LoadAsync(scope, new[] { device1, device2 }, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Items);
        Assert.True(result.Errors.ContainsKey(device1Id));
        Assert.Contains("Network error", result.Errors[device1Id]);
    }
}

public class EvidenceLoader_LoadAsync_DeviceNameFallback_Tests
{
    [Fact]
    public async Task LoadAsync_WithDeviceIdNotInAllDevices_FallbackToFirstEightCharsOfId()
    {
        // Arrange
        var unknownDeviceId = Guid.NewGuid();
        var scope = new ForensicScope(
            DeviceIds: new[] { unknownDeviceId },
            FromUtc: new DateTime(2026, 9, 17, 0, 0, 0, DateTimeKind.Utc),
            ToUtc: new DateTime(2026, 9, 24, 0, 0, 0, DateTimeKind.Utc),
            Search: null);

        // allDevices is empty, so unknownDeviceId won't be found
        var allDevices = new List<Device>();

        var testEvent = new Event
        {
            Id = Guid.NewGuid(),
            DeviceId = unknownDeviceId,
            EventType = "Motion",
            OccurredAtUtc = new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc),
            DiscoveredAtUtc = new DateTime(2026, 9, 20, 12, 5, 0, DateTimeKind.Utc),
            ProviderEventId = "provider-event-1",
            MetadataJson = "{}"
        };

        var mockEventRepo = new Mock<IEventRepository>();
        mockEventRepo
            .Setup(r => r.ListByDeviceAndDateRangeAsync(unknownDeviceId, scope.FromUtc, scope.ToUtc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { testEvent });

        var mockReportService = new Mock<IReportGenerationService>();
        mockReportService
            .Setup(r => r.BuildEvidenceReviewAsync(unknownDeviceId, scope.FromUtc, scope.ToUtc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EvidenceReviewReport { MediaItems = new List<MediaItem>(), IntegrityRecords = new List<IntegrityRecord>() });

        var mockHoldRepo = new Mock<ILegalHoldRepository>();
        mockHoldRepo
            .Setup(r => r.GetActiveByMediaItemIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LegalHold>());

        var mockMediaRepo = new Mock<IMediaItemRepository>();
        mockMediaRepo
            .Setup(r => r.GetByDeviceAndDateRangeAsync(unknownDeviceId, scope.FromUtc, scope.ToUtc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MediaItem>());

        var eventRowLoader = new EventRowLoader(mockEventRepo.Object, mockReportService.Object, mockHoldRepo.Object);
        var loader = new EvidenceLoader(eventRowLoader, mockMediaRepo.Object);

        // Act
        var result = await loader.LoadAsync(scope, allDevices, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Items);
        var item = result.Items[0];
        var expectedFallbackName = unknownDeviceId.ToString("D")[..8];
        Assert.Equal(expectedFallbackName, item.DeviceName);
    }

    [Fact]
    public async Task LoadAsync_WithUnknownDeviceIdForMediaOnly_FallbackToFirstEightCharsOfId()
    {
        // Arrange
        var unknownDeviceId = Guid.NewGuid();
        var scope = new ForensicScope(
            DeviceIds: new[] { unknownDeviceId },
            FromUtc: new DateTime(2026, 9, 17, 0, 0, 0, DateTimeKind.Utc),
            ToUtc: new DateTime(2026, 9, 24, 0, 0, 0, DateTimeKind.Utc),
            Search: null);

        // allDevices is empty
        var allDevices = new List<Device>();

        var mediaItem = new MediaItem
        {
            Id = Guid.NewGuid(),
            DeviceId = unknownDeviceId,
            FileName = "snapshot.jpg",
            FilePath = "/path/snapshot.jpg",
            MediaFormat = "image/jpeg",
            Sha256Hash = "hash",
            RecordedAtUtc = new DateTime(2026, 9, 20, 12, 30, 0, DateTimeKind.Utc),
            DownloadedAtUtc = new DateTime(2026, 9, 20, 12, 35, 0, DateTimeKind.Utc),
            IsPurged = false
        };

        var mockEventRepo = new Mock<IEventRepository>();
        mockEventRepo
            .Setup(r => r.ListByDeviceAndDateRangeAsync(unknownDeviceId, scope.FromUtc, scope.ToUtc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Event>());

        var mockReportService = new Mock<IReportGenerationService>();
        mockReportService
            .Setup(r => r.BuildEvidenceReviewAsync(unknownDeviceId, scope.FromUtc, scope.ToUtc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EvidenceReviewReport { MediaItems = new List<MediaItem>(), IntegrityRecords = new List<IntegrityRecord>() });

        var mockHoldRepo = new Mock<ILegalHoldRepository>();
        mockHoldRepo
            .Setup(r => r.GetActiveByMediaItemIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LegalHold>());

        var mockMediaRepo = new Mock<IMediaItemRepository>();
        mockMediaRepo
            .Setup(r => r.GetByDeviceAndDateRangeAsync(unknownDeviceId, scope.FromUtc, scope.ToUtc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { mediaItem });

        var eventRowLoader = new EventRowLoader(mockEventRepo.Object, mockReportService.Object, mockHoldRepo.Object);
        var loader = new EvidenceLoader(eventRowLoader, mockMediaRepo.Object);

        // Act
        var result = await loader.LoadAsync(scope, allDevices, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Items);
        var item = result.Items[0];
        var expectedFallbackName = unknownDeviceId.ToString("D")[..8];
        Assert.Equal(expectedFallbackName, item.DeviceName);
    }
}

public class EvidenceLoader_LoadAsync_SearchFiltering_Tests
{
    [Fact]
    public async Task LoadAsync_WithSearch_FiltersMediaOnlyItemsByDeviceName()
    {
        // Arrange
        var device1Id = Guid.NewGuid();
        var device2Id = Guid.NewGuid();
        var device1 = new Device
        {
            Id = device1Id,
            LocationId = Guid.NewGuid(),
            Name = "FrontDoor",
            ProviderDeviceId = "provider-id",
            Type = "camera",
            IsOnline = true,
            MetadataJson = "{}"
        };

        var device2 = new Device
        {
            Id = device2Id,
            LocationId = Guid.NewGuid(),
            Name = "BackYard",
            ProviderDeviceId = "provider-id-2",
            Type = "camera",
            IsOnline = true,
            MetadataJson = "{}"
        };

        var scope = new ForensicScope(
            DeviceIds: new[] { device1Id, device2Id },
            FromUtc: new DateTime(2026, 9, 17, 0, 0, 0, DateTimeKind.Utc),
            ToUtc: new DateTime(2026, 9, 24, 0, 0, 0, DateTimeKind.Utc),
            Search: "FrontDoor");

        var matchingMedia = new MediaItem
        {
            Id = Guid.NewGuid(),
            DeviceId = device1Id,
            FileName = "snapshot.jpg",
            FilePath = "/path/snapshot.jpg",
            MediaFormat = "image/jpeg",
            Sha256Hash = "hash1",
            RecordedAtUtc = new DateTime(2026, 9, 20, 12, 30, 0, DateTimeKind.Utc),
            DownloadedAtUtc = new DateTime(2026, 9, 20, 12, 35, 0, DateTimeKind.Utc),
            IsPurged = false
        };

        var nonMatchingMedia = new MediaItem
        {
            Id = Guid.NewGuid(),
            DeviceId = device2Id,
            FileName = "snapshot.jpg",
            FilePath = "/path/snapshot.jpg",
            MediaFormat = "image/jpeg",
            Sha256Hash = "hash2",
            RecordedAtUtc = new DateTime(2026, 9, 20, 12, 40, 0, DateTimeKind.Utc),
            DownloadedAtUtc = new DateTime(2026, 9, 20, 12, 45, 0, DateTimeKind.Utc),
            IsPurged = false
        };

        var mockEventRepo = new Mock<IEventRepository>();
        mockEventRepo
            .Setup(r => r.ListByDeviceAndDateRangeAsync(It.IsAny<Guid>(), scope.FromUtc, scope.ToUtc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Event>());

        var mockReportService = new Mock<IReportGenerationService>();
        mockReportService
            .Setup(r => r.BuildEvidenceReviewAsync(It.IsAny<Guid>(), scope.FromUtc, scope.ToUtc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EvidenceReviewReport { MediaItems = new List<MediaItem>(), IntegrityRecords = new List<IntegrityRecord>() });

        var mockHoldRepo = new Mock<ILegalHoldRepository>();
        mockHoldRepo
            .Setup(r => r.GetActiveByMediaItemIdsAsync(It.IsAny<IReadOnlyList<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LegalHold>());

        var mockMediaRepo = new Mock<IMediaItemRepository>();
        mockMediaRepo
            .Setup(r => r.GetByDeviceAndDateRangeAsync(device1Id, scope.FromUtc, scope.ToUtc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { matchingMedia });
        mockMediaRepo
            .Setup(r => r.GetByDeviceAndDateRangeAsync(device2Id, scope.FromUtc, scope.ToUtc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { nonMatchingMedia });

        var eventRowLoader = new EventRowLoader(mockEventRepo.Object, mockReportService.Object, mockHoldRepo.Object);
        var loader = new EvidenceLoader(eventRowLoader, mockMediaRepo.Object);

        // Act
        var result = await loader.LoadAsync(scope, new[] { device1, device2 }, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Items);
        var item = result.Items[0];
        Assert.Equal(matchingMedia.Id, item.Media!.Id);
    }
}
