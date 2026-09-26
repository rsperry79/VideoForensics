namespace VideoForensics.Ui.Shared.Tests.Services.Events;

using Xunit;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Ui.Shared.Services.Events;
using VideoForensics.Ui.Shared.Services.Inspector;

public class EventInspectorMapper_ToInspector_Tests
{
    [Fact]
    public void ToInspector_BuildsCorrectTitle()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var @event = new Event
        {
            Id = eventId,
            DeviceId = deviceId,
            EventType = "Motion",
            OccurredAtUtc = new DateTime(2026, 9, 20, 14, 30, 45, DateTimeKind.Utc),
            DiscoveredAtUtc = new DateTime(2026, 9, 20, 14, 35, 0, DateTimeKind.Utc),
            ProviderEventId = "provider-event-1",
            MetadataJson = null
        };

        var row = new EventRow
        {
            Event = @event,
            MediaItem = null,
            DeviceId = deviceId
        };

        var holds = new Dictionary<Guid, LegalHold>();
        var integrityRecords = new List<IntegrityRecord>();

        // Act
        var inspector = EventInspectorMapper.ToInspector(row, holds, integrityRecords);

        // Assert
        Assert.Contains("Motion", inspector.Title);
        Assert.Contains("2026-09-20", inspector.Title);
        Assert.Contains("14:30:45", inspector.Title);
    }

    [Fact]
    public void ToInspector_IncludesEventFields()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var @event = new Event
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            EventType = "Alert",
            OccurredAtUtc = new DateTime(2026, 9, 20, 14, 30, 45, DateTimeKind.Utc),
            DiscoveredAtUtc = new DateTime(2026, 9, 20, 14, 35, 0, DateTimeKind.Utc),
            ProviderEventId = "provider-event-1",
            MetadataJson = "{\"test\": \"data\"}"
        };

        var row = new EventRow
        {
            Event = @event,
            MediaItem = null,
            DeviceId = deviceId
        };

        var holds = new Dictionary<Guid, LegalHold>();
        var integrityRecords = new List<IntegrityRecord>();

        // Act
        var inspector = EventInspectorMapper.ToInspector(row, holds, integrityRecords);

        // Assert
        Assert.NotNull(inspector.Fields);
    }

    [Fact]
    public void ToInspector_WithEventMetadataJson_SetsRawJson()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var eventJson = "{\"event\": \"data\"}";
        var @event = new Event
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            EventType = "Motion",
            OccurredAtUtc = DateTime.UtcNow,
            DiscoveredAtUtc = DateTime.UtcNow,
            ProviderEventId = "provider-event-1",
            MetadataJson = eventJson
        };

        var row = new EventRow
        {
            Event = @event,
            MediaItem = null,
            DeviceId = deviceId
        };

        var holds = new Dictionary<Guid, LegalHold>();
        var integrityRecords = new List<IntegrityRecord>();

        // Act
        var inspector = EventInspectorMapper.ToInspector(row, holds, integrityRecords);

        // Assert
        Assert.Equal(eventJson, inspector.RawJson);
    }

    [Fact]
    public void ToInspector_WithMediaMetadataJson_UsesMediaWhenEventJsonNull()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var mediaJson = "{\"media\": \"data\"}";
        var @event = new Event
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            EventType = "Motion",
            OccurredAtUtc = DateTime.UtcNow,
            DiscoveredAtUtc = DateTime.UtcNow,
            ProviderEventId = "provider-event-1",
            MetadataJson = null
        };

        var media = new MediaItem
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            DownloadEventId = @event.Id,
            FileName = "test.mp4",
            FilePath = "/media/test.mp4",
            MediaFormat = "mp4",
            Resolution = "1920x1080",
            FileSizeBytes = 1024,
            DownloadedAtUtc = DateTime.UtcNow,
            Sha256Hash = "abc123",
            MetadataJson = mediaJson
        };

        var row = new EventRow
        {
            Event = @event,
            MediaItem = media,
            DeviceId = deviceId
        };

        var holds = new Dictionary<Guid, LegalHold>();
        var integrityRecords = new List<IntegrityRecord>();

        // Act
        var inspector = EventInspectorMapper.ToInspector(row, holds, integrityRecords);

        // Assert
        Assert.Equal(mediaJson, inspector.RawJson);
    }

    [Fact]
    public void ToInspector_WithBothJsons_PrefersEvent()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var eventJson = "{\"event\": \"data\"}";
        var mediaJson = "{\"media\": \"data\"}";
        var @event = new Event
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            EventType = "Motion",
            OccurredAtUtc = DateTime.UtcNow,
            DiscoveredAtUtc = DateTime.UtcNow,
            ProviderEventId = "provider-event-1",
            MetadataJson = eventJson
        };

        var media = new MediaItem
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            DownloadEventId = @event.Id,
            FileName = "test.mp4",
            FilePath = "/media/test.mp4",
            MediaFormat = "mp4",
            Resolution = "1920x1080",
            FileSizeBytes = 1024,
            DownloadedAtUtc = DateTime.UtcNow,
            Sha256Hash = "abc123",
            MetadataJson = mediaJson
        };

        var row = new EventRow
        {
            Event = @event,
            MediaItem = media,
            DeviceId = deviceId
        };

        var holds = new Dictionary<Guid, LegalHold>();
        var integrityRecords = new List<IntegrityRecord>();

        // Act
        var inspector = EventInspectorMapper.ToInspector(row, holds, integrityRecords);

        // Assert
        Assert.Equal(eventJson, inspector.RawJson);
    }

    [Fact]
    public void ToInspector_ProvenanceIncludesProviderEventId()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var @event = new Event
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            EventType = "Motion",
            OccurredAtUtc = DateTime.UtcNow,
            DiscoveredAtUtc = new DateTime(2026, 9, 20, 14, 35, 0, DateTimeKind.Utc),
            DownloadedAtUtc = new DateTime(2026, 9, 20, 14, 40, 0, DateTimeKind.Utc),
            ProviderEventId = "provider-event-123",
            MetadataJson = null
        };

        var row = new EventRow
        {
            Event = @event,
            MediaItem = null,
            DeviceId = deviceId
        };

        var holds = new Dictionary<Guid, LegalHold>();
        var integrityRecords = new List<IntegrityRecord>();

        // Act
        var inspector = EventInspectorMapper.ToInspector(row, holds, integrityRecords);

        // Assert
        Assert.NotNull(inspector.Provenance);
        Assert.Contains(inspector.Provenance, kvp => kvp.Key == "ProviderEventId" && kvp.Value == "provider-event-123");
    }

    [Fact]
    public void ToInspector_ProvenanceIncludesDiscoveredAndDownloadedTimes()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var discoveredAt = new DateTime(2026, 9, 20, 14, 35, 0, DateTimeKind.Utc);
        var downloadedAt = new DateTime(2026, 9, 20, 14, 40, 0, DateTimeKind.Utc);

        var @event = new Event
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            EventType = "Motion",
            OccurredAtUtc = DateTime.UtcNow,
            DiscoveredAtUtc = discoveredAt,
            DownloadedAtUtc = downloadedAt,
            ProviderEventId = "provider-event-1",
            MetadataJson = null
        };

        var row = new EventRow
        {
            Event = @event,
            MediaItem = null,
            DeviceId = deviceId
        };

        var holds = new Dictionary<Guid, LegalHold>();
        var integrityRecords = new List<IntegrityRecord>();

        // Act
        var inspector = EventInspectorMapper.ToInspector(row, holds, integrityRecords);

        // Assert
        Assert.NotNull(inspector.Provenance);
        var provenanceDict = inspector.Provenance.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        Assert.Contains("DiscoveredAtUtc", provenanceDict.Keys);
        Assert.Contains("DownloadedAtUtc", provenanceDict.Keys);
    }

    [Fact]
    public void ToInspector_ProvenanceIncludesMediaInfoWhenPresent()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var mediaItemId = Guid.NewGuid();
        var @event = new Event
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            EventType = "Motion",
            OccurredAtUtc = DateTime.UtcNow,
            DiscoveredAtUtc = DateTime.UtcNow,
            ProviderEventId = "provider-event-1",
            MetadataJson = null
        };

        var media = new MediaItem
        {
            Id = mediaItemId,
            DeviceId = deviceId,
            DownloadEventId = @event.Id,
            FileName = "test.mp4",
            FilePath = "/media/test.mp4",
            MediaFormat = "mp4",
            Resolution = "1920x1080",
            FileSizeBytes = 1024,
            DownloadedAtUtc = DateTime.UtcNow,
            MetadataJson = null,
            Sha256Hash = "abc123",
            IntegrityVerified = true,
            LastVerifiedAtUtc = DateTime.UtcNow,
            ApiSourceHash = "hash123"
        };

        var row = new EventRow
        {
            Event = @event,
            MediaItem = media,
            DeviceId = deviceId
        };

        var holds = new Dictionary<Guid, LegalHold>();
        var integrityRecords = new List<IntegrityRecord>();

        // Act
        var inspector = EventInspectorMapper.ToInspector(row, holds, integrityRecords);

        // Assert
        Assert.NotNull(inspector.Provenance);
        var provenanceDict = inspector.Provenance.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        Assert.Contains("Sha256Hash", provenanceDict.Keys);
        Assert.Contains("IntegrityVerified", provenanceDict.Keys);
    }

    [Fact]
    public void ToInspector_SetsPin_ToEventAndEventId()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var @event = new Event
        {
            Id = eventId,
            DeviceId = deviceId,
            EventType = "Motion",
            OccurredAtUtc = DateTime.UtcNow,
            DiscoveredAtUtc = DateTime.UtcNow,
            ProviderEventId = "provider-event-1",
            MetadataJson = null
        };

        var row = new EventRow
        {
            Event = @event,
            MediaItem = null,
            DeviceId = deviceId
        };

        var holds = new Dictionary<Guid, LegalHold>();
        var integrityRecords = new List<IntegrityRecord>();

        // Act
        var inspector = EventInspectorMapper.ToInspector(row, holds, integrityRecords);

        // Assert - an event row always pins to its Event, even when media is attached.
        Assert.NotNull(inspector.Pin);
        Assert.Equal(CaseItemKind.Event, inspector.Pin!.Kind);
        Assert.Equal(eventId, inspector.Pin.TargetId);
    }

    [Fact]
    public void ToInspector_WithMediaItem_StillPinsToEventNotMedia()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var mediaId = Guid.NewGuid();
        var @event = new Event
        {
            Id = eventId,
            DeviceId = deviceId,
            EventType = "Motion",
            OccurredAtUtc = DateTime.UtcNow,
            DiscoveredAtUtc = DateTime.UtcNow,
            ProviderEventId = "provider-event-1",
            MetadataJson = null
        };

        var media = new MediaItem
        {
            Id = mediaId,
            DeviceId = deviceId,
            DownloadEventId = eventId,
            FileName = "test.mp4",
            FilePath = "/media/test.mp4",
            MediaFormat = "mp4",
            FileSizeBytes = 1024,
            DownloadedAtUtc = DateTime.UtcNow,
            Sha256Hash = "abc123"
        };

        var row = new EventRow
        {
            Event = @event,
            MediaItem = media,
            DeviceId = deviceId
        };

        var holds = new Dictionary<Guid, LegalHold>();
        var integrityRecords = new List<IntegrityRecord>();

        // Act
        var inspector = EventInspectorMapper.ToInspector(row, holds, integrityRecords);

        // Assert
        Assert.NotNull(inspector.Pin);
        Assert.Equal(CaseItemKind.Event, inspector.Pin!.Kind);
        Assert.Equal(eventId, inspector.Pin.TargetId);
    }
}
