namespace VideoForensics.Ui.Shared.Tests.Services.Events;

using Xunit;
using Moq;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Contracts;
using VideoForensics.Data.Core.Models;
using VideoForensics.Client.Common.Contracts;
using VideoForensics.Ui.Shared.Services.Events;
using VideoForensics.Ui.Shared.Services.Scope;

public class EventRowLoader_LoadAsync_SingleDevice_Tests
{
    [Fact]
    public async Task LoadAsync_SingleDevice_ReturnsEventRows()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
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

        var loader = new EventRowLoader(mockEventRepo.Object, mockReportService.Object, mockHoldRepo.Object);

        // Act
        var result = await loader.LoadAsync(scope, new[] { deviceId }, false, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Rows);
        Assert.Equal("Motion", result.Rows[0].EventType);
        Assert.Equal(deviceId, result.Rows[0].DeviceId);
    }

    [Fact]
    public async Task LoadAsync_SortsByOccurredAtDescending()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var scope = new ForensicScope(
            DeviceIds: new[] { deviceId },
            FromUtc: new DateTime(2026, 9, 17, 0, 0, 0, DateTimeKind.Utc),
            ToUtc: new DateTime(2026, 9, 24, 0, 0, 0, DateTimeKind.Utc),
            Search: null);

        var event1 = new Event
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            EventType = "Motion",
            OccurredAtUtc = new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc),
            DiscoveredAtUtc = new DateTime(2026, 9, 20, 12, 5, 0, DateTimeKind.Utc),
            ProviderEventId = "event-1",
            MetadataJson = "{}"
        };

        var event2 = new Event
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            EventType = "Motion",
            OccurredAtUtc = new DateTime(2026, 9, 21, 12, 0, 0, DateTimeKind.Utc),
            DiscoveredAtUtc = new DateTime(2026, 9, 21, 12, 5, 0, DateTimeKind.Utc),
            ProviderEventId = "event-2",
            MetadataJson = "{}"
        };

        var mockEventRepo = new Mock<IEventRepository>();
        mockEventRepo
            .Setup(r => r.ListByDeviceAndDateRangeAsync(deviceId, scope.FromUtc, scope.ToUtc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { event1, event2 });

        var mockReportService = new Mock<IReportGenerationService>();
        mockReportService
            .Setup(r => r.BuildEvidenceReviewAsync(deviceId, scope.FromUtc, scope.ToUtc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EvidenceReviewReport { MediaItems = new List<MediaItem>(), IntegrityRecords = new List<IntegrityRecord>() });

        var mockHoldRepo = new Mock<ILegalHoldRepository>();

        var loader = new EventRowLoader(mockEventRepo.Object, mockReportService.Object, mockHoldRepo.Object);

        // Act
        var result = await loader.LoadAsync(scope, new[] { deviceId }, false, CancellationToken.None);

        // Assert
        Assert.Equal(2, result.Rows.Count);
        Assert.Equal(new DateTime(2026, 9, 21, 12, 0, 0, DateTimeKind.Utc), result.Rows[0].OccurredAtUtc);
        Assert.Equal(new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc), result.Rows[1].OccurredAtUtc);
    }
}

public class EventRowLoader_LoadAsync_MultiDevice_Tests
{
    [Fact]
    public async Task LoadAsync_MultipleDevices_FansOutAndMerges()
    {
        // Arrange
        var device1Id = Guid.NewGuid();
        var device2Id = Guid.NewGuid();
        var scope = new ForensicScope(
            DeviceIds: new[] { device1Id, device2Id },
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
            EventType = "Alert",
            OccurredAtUtc = new DateTime(2026, 9, 21, 12, 0, 0, DateTimeKind.Utc),
            DiscoveredAtUtc = new DateTime(2026, 9, 21, 12, 5, 0, DateTimeKind.Utc),
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

        var loader = new EventRowLoader(mockEventRepo.Object, mockReportService.Object, mockHoldRepo.Object);

        // Act
        var result = await loader.LoadAsync(scope, new[] { device1Id, device2Id }, false, CancellationToken.None);

        // Assert
        Assert.Equal(2, result.Rows.Count);
        mockEventRepo.Verify(r => r.ListByDeviceAndDateRangeAsync(device1Id, scope.FromUtc, scope.ToUtc, It.IsAny<CancellationToken>()), Times.Once);
        mockEventRepo.Verify(r => r.ListByDeviceAndDateRangeAsync(device2Id, scope.FromUtc, scope.ToUtc, It.IsAny<CancellationToken>()), Times.Once);
    }
}

public class EventRowLoader_LoadAsync_AllDevices_Tests
{
    [Fact]
    public async Task LoadAsync_AllDevicesScope_UsesAllDevicesFromParameter()
    {
        // Arrange
        var device1Id = Guid.NewGuid();
        var device2Id = Guid.NewGuid();
        var scope = new ForensicScope(
            DeviceIds: new List<Guid>(), // All devices
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

        var mockEventRepo = new Mock<IEventRepository>();
        mockEventRepo
            .Setup(r => r.ListByDeviceAndDateRangeAsync(device1Id, scope.FromUtc, scope.ToUtc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { event1 });
        mockEventRepo
            .Setup(r => r.ListByDeviceAndDateRangeAsync(device2Id, scope.FromUtc, scope.ToUtc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Event>());

        var mockReportService = new Mock<IReportGenerationService>();
        mockReportService
            .Setup(r => r.BuildEvidenceReviewAsync(It.IsAny<Guid>(), scope.FromUtc, scope.ToUtc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EvidenceReviewReport { MediaItems = new List<MediaItem>(), IntegrityRecords = new List<IntegrityRecord>() });

        var mockHoldRepo = new Mock<ILegalHoldRepository>();

        var loader = new EventRowLoader(mockEventRepo.Object, mockReportService.Object, mockHoldRepo.Object);

        // Act
        var result = await loader.LoadAsync(scope, new[] { device1Id, device2Id }, false, CancellationToken.None);

        // Assert
        Assert.Single(result.Rows);
        mockEventRepo.Verify(r => r.ListByDeviceAndDateRangeAsync(device1Id, scope.FromUtc, scope.ToUtc, It.IsAny<CancellationToken>()), Times.Once);
        mockEventRepo.Verify(r => r.ListByDeviceAndDateRangeAsync(device2Id, scope.FromUtc, scope.ToUtc, It.IsAny<CancellationToken>()), Times.Once);
    }
}

public class EventRowLoader_LoadAsync_SearchFilter_Tests
{
    [Fact]
    public async Task LoadAsync_WithSearch_FiltersRows()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var scope = new ForensicScope(
            DeviceIds: new[] { deviceId },
            FromUtc: new DateTime(2026, 9, 17, 0, 0, 0, DateTimeKind.Utc),
            ToUtc: new DateTime(2026, 9, 24, 0, 0, 0, DateTimeKind.Utc),
            Search: "motion");

        var event1 = new Event
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            EventType = "Motion",
            OccurredAtUtc = new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc),
            DiscoveredAtUtc = new DateTime(2026, 9, 20, 12, 5, 0, DateTimeKind.Utc),
            ProviderEventId = "event-1",
            MetadataJson = "{}"
        };

        var event2 = new Event
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            EventType = "Alert",
            OccurredAtUtc = new DateTime(2026, 9, 21, 12, 0, 0, DateTimeKind.Utc),
            DiscoveredAtUtc = new DateTime(2026, 9, 21, 12, 5, 0, DateTimeKind.Utc),
            ProviderEventId = "event-2",
            MetadataJson = "{}"
        };

        var mockEventRepo = new Mock<IEventRepository>();
        mockEventRepo
            .Setup(r => r.ListByDeviceAndDateRangeAsync(deviceId, scope.FromUtc, scope.ToUtc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { event1, event2 });

        var mockReportService = new Mock<IReportGenerationService>();
        mockReportService
            .Setup(r => r.BuildEvidenceReviewAsync(deviceId, scope.FromUtc, scope.ToUtc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EvidenceReviewReport { MediaItems = new List<MediaItem>(), IntegrityRecords = new List<IntegrityRecord>() });

        var mockHoldRepo = new Mock<ILegalHoldRepository>();

        var loader = new EventRowLoader(mockEventRepo.Object, mockReportService.Object, mockHoldRepo.Object);

        // Act
        var result = await loader.LoadAsync(scope, new[] { deviceId }, false, CancellationToken.None);

        // Assert
        Assert.Single(result.Rows);
        Assert.Equal("Motion", result.Rows[0].EventType);
    }
}

public class EventRowLoader_LoadAsync_UnansweredMode_Tests
{
    [Fact]
    public async Task LoadAsync_UnansweredMode_SkipsEvidenceReviewJoin()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
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
            ProviderEventId = "event-1",
            MetadataJson = "{}"
        };

        var mockEventRepo = new Mock<IEventRepository>();
        mockEventRepo
            .Setup(r => r.ListUnansweredOrFlaggedAsync(deviceId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { testEvent });

        var mockReportService = new Mock<IReportGenerationService>();
        var mockHoldRepo = new Mock<ILegalHoldRepository>();

        var loader = new EventRowLoader(mockEventRepo.Object, mockReportService.Object, mockHoldRepo.Object);

        // Act
        var result = await loader.LoadAsync(scope, new[] { deviceId }, true, CancellationToken.None);

        // Assert
        Assert.Single(result.Rows);
        mockReportService.Verify(r => r.BuildEvidenceReviewAsync(It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

public class EventRowLoader_LoadAsync_ErrorHandling_Tests
{
    [Fact]
    public async Task LoadAsync_OneDeviceThrows_OtherDevicesLoad()
    {
        // Arrange
        var device1Id = Guid.NewGuid();
        var device2Id = Guid.NewGuid();
        var scope = new ForensicScope(
            DeviceIds: new[] { device1Id, device2Id },
            FromUtc: new DateTime(2026, 9, 17, 0, 0, 0, DateTimeKind.Utc),
            ToUtc: new DateTime(2026, 9, 24, 0, 0, 0, DateTimeKind.Utc),
            Search: null);

        var event2 = new Event
        {
            Id = Guid.NewGuid(),
            DeviceId = device2Id,
            EventType = "Alert",
            OccurredAtUtc = new DateTime(2026, 9, 21, 12, 0, 0, DateTimeKind.Utc),
            DiscoveredAtUtc = new DateTime(2026, 9, 21, 12, 5, 0, DateTimeKind.Utc),
            ProviderEventId = "event-2",
            MetadataJson = "{}"
        };

        var mockEventRepo = new Mock<IEventRepository>();
        mockEventRepo
            .Setup(r => r.ListByDeviceAndDateRangeAsync(device1Id, scope.FromUtc, scope.ToUtc, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Test error"));
        mockEventRepo
            .Setup(r => r.ListByDeviceAndDateRangeAsync(device2Id, scope.FromUtc, scope.ToUtc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { event2 });

        var mockReportService = new Mock<IReportGenerationService>();
        mockReportService
            .Setup(r => r.BuildEvidenceReviewAsync(device2Id, scope.FromUtc, scope.ToUtc, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EvidenceReviewReport { MediaItems = new List<MediaItem>(), IntegrityRecords = new List<IntegrityRecord>() });

        var mockHoldRepo = new Mock<ILegalHoldRepository>();

        var loader = new EventRowLoader(mockEventRepo.Object, mockReportService.Object, mockHoldRepo.Object);

        // Act
        var result = await loader.LoadAsync(scope, new[] { device1Id, device2Id }, false, CancellationToken.None);

        // Assert
        Assert.Single(result.Rows);
        Assert.Contains(device1Id, result.PerDeviceErrors.Keys);
    }
}
