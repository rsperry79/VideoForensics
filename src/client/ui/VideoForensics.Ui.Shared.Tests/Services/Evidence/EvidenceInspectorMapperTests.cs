using Xunit;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Ui.Shared.Services.Evidence;
using VideoForensics.Ui.Shared.Services.Inspector;

namespace VideoForensics.Ui.Shared.Tests.Services.Evidence;

public class EvidenceInspectorMapper_ToInspector_EventItem_Tests
{
    [Fact]
    public void ToInspector_EventItem_SetsTitleAsEventTypeAndOccurredAtUtc()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var eventItem = new EvidenceItem
        {
            Key = "event:123",
            Kind = EvidenceKind.Event,
            OccurredAtUtc = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc),
            DeviceId = deviceId,
            DeviceName = "Front Door",
            Event = new Event { Id = Guid.NewGuid(), EventType = "Motion Detected", ProviderEventId = "event:1", MetadataJson = "{}" },
            Media = null
        };
        var holds = new Dictionary<Guid, LegalHold>();
        var integrity = new List<IntegrityRecord>();

        // Act
        var result = EvidenceInspectorMapper.ToInspector(eventItem, holds, integrity);

        // Assert
        Assert.Contains("Motion Detected", result.Title);
        Assert.Contains("2024-01-15", result.Title);
        Assert.Contains("10:30:00", result.Title);
    }

    [Fact]
    public void ToInspector_EventItem_IncludesEventInFields()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var evt = new Event { Id = Guid.NewGuid(), EventType = "Motion", ProviderEventId = "event:2", MetadataJson = "{\"test\": true}" };
        var eventItem = new EvidenceItem
        {
            Key = "event:123",
            Kind = EvidenceKind.Event,
            OccurredAtUtc = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc),
            DeviceId = deviceId,
            DeviceName = "Front Door",
            Event = evt,
            Media = null
        };
        var holds = new Dictionary<Guid, LegalHold>();
        var integrity = new List<IntegrityRecord>();

        // Act
        var result = EvidenceInspectorMapper.ToInspector(eventItem, holds, integrity);

        // Assert
        Assert.NotNull(result.Fields);
        var fieldsType = result.Fields.GetType();
        var eventProperty = fieldsType.GetProperty("Event");
        Assert.NotNull(eventProperty);
        Assert.Equal(evt, eventProperty?.GetValue(result.Fields));
    }

    [Fact]
    public void ToInspector_MediaOnlyItem_SetsTitleAsLabelDeviceNameAndOccurredAtUtc()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var mediaId = Guid.NewGuid();
        var media = new MediaItem
        {
            Id = mediaId,
            DeviceId = deviceId,
            FileName = "snapshot.jpg",
            FilePath = "/path",
            MediaFormat = "image/jpeg",
            FileSizeBytes = 123456,
            RecordedAtUtc = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc),
            DownloadedAtUtc = new DateTime(2024, 1, 15, 10, 35, 0, DateTimeKind.Utc),
            Sha256Hash = "abc123",
            IntegrityVerified = false
        };
        var mediaItem = new EvidenceItem
        {
            Key = $"media:{mediaId}",
            Kind = EvidenceKind.Snapshot,
            OccurredAtUtc = media.RecordedAtUtc,
            DeviceId = deviceId,
            DeviceName = "Doorbell",
            Event = null,
            Media = media
        };
        var holds = new Dictionary<Guid, LegalHold>();
        var integrity = new List<IntegrityRecord>();

        // Act
        var result = EvidenceInspectorMapper.ToInspector(mediaItem, holds, integrity);

        // Assert
        Assert.Contains("Snapshot", result.Title);
        Assert.Contains("Doorbell", result.Title);
        Assert.Contains("2024-01-15", result.Title);
        Assert.Contains("10:30:00", result.Title);
    }

    [Fact]
    public void ToInspector_MediaItem_IncludesMediaInFields()
    {
        // Arrange
        var mediaId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var media = new MediaItem
        {
            Id = mediaId,
            DeviceId = deviceId,
            FileName = "video.mp4",
            FilePath = "/path",
            MediaFormat = "video/mp4",
            FileSizeBytes = 5000000,
            RecordedAtUtc = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc),
            DownloadedAtUtc = new DateTime(2024, 1, 15, 10, 35, 0, DateTimeKind.Utc),
            Sha256Hash = "def456",
            IntegrityVerified = true,
            LastVerifiedAtUtc = new DateTime(2024, 1, 16, 10, 30, 0, DateTimeKind.Utc),
            ApiSourceHash = "src123"
        };
        var mediaItem = new EvidenceItem
        {
            Key = $"media:{mediaId}",
            Kind = EvidenceKind.Video,
            OccurredAtUtc = media.RecordedAtUtc,
            DeviceId = deviceId,
            DeviceName = "Garage",
            Event = null,
            Media = media
        };
        var holds = new Dictionary<Guid, LegalHold>();
        var integrity = new List<IntegrityRecord>();

        // Act
        var result = EvidenceInspectorMapper.ToInspector(mediaItem, holds, integrity);

        // Assert
        Assert.NotNull(result.Fields);
        var fieldsType = result.Fields.GetType();
        var mediaProperty = fieldsType.GetProperty("MediaItem");
        Assert.NotNull(mediaProperty);
        Assert.Equal(media, mediaProperty?.GetValue(result.Fields));
    }

    [Fact]
    public void ToInspector_IncludesMediaProvenance()
    {
        // Arrange
        var mediaId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var media = new MediaItem
        {
            Id = mediaId,
            DeviceId = deviceId,
            FileName = "image.png",
            FilePath = "/path",
            MediaFormat = "image/png",
            FileSizeBytes = 250000,
            RecordedAtUtc = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc),
            DownloadedAtUtc = new DateTime(2024, 1, 15, 10, 35, 0, DateTimeKind.Utc),
            Sha256Hash = "ghi789",
            IntegrityVerified = true,
            LastVerifiedAtUtc = new DateTime(2024, 1, 16, 10, 30, 0, DateTimeKind.Utc),
            ApiSourceHash = "src456"
        };
        var mediaItem = new EvidenceItem
        {
            Key = $"media:{mediaId}",
            Kind = EvidenceKind.Snapshot,
            OccurredAtUtc = media.RecordedAtUtc,
            DeviceId = deviceId,
            DeviceName = "Porch",
            Event = null,
            Media = media
        };
        var holds = new Dictionary<Guid, LegalHold>();
        var integrity = new List<IntegrityRecord>();

        // Act
        var result = EvidenceInspectorMapper.ToInspector(mediaItem, holds, integrity);

        // Assert
        Assert.NotNull(result.Provenance);
        var provenanceDict = result.Provenance.ToDictionary(x => x.Key, x => x.Value);

        Assert.Equal("ghi789", provenanceDict["Sha256Hash"]);
        Assert.Equal("True", provenanceDict["IntegrityVerified"]);
        Assert.Contains("2024-01-16", provenanceDict["LastVerifiedAtUtc"]);
        Assert.Contains("10:30:00", provenanceDict["LastVerifiedAtUtc"]);
        Assert.Equal("src456", provenanceDict["ApiSourceHash"]);
    }

    [Fact]
    public void ToInspector_MediaOnHold_IncludesLegalHoldStatus()
    {
        // Arrange
        var mediaId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var media = new MediaItem
        {
            Id = mediaId,
            DeviceId = deviceId,
            FileName = "video.mp4",
            FilePath = "/path",
            MediaFormat = "video/mp4",
            FileSizeBytes = 1000000,
            RecordedAtUtc = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc),
            DownloadedAtUtc = new DateTime(2024, 1, 15, 10, 35, 0, DateTimeKind.Utc),
            Sha256Hash = "xyz789",
            IntegrityVerified = false
        };
        var mediaItem = new EvidenceItem
        {
            Key = $"media:{mediaId}",
            Kind = EvidenceKind.Video,
            OccurredAtUtc = media.RecordedAtUtc,
            DeviceId = deviceId,
            DeviceName = "Driveway",
            Event = null,
            Media = media
        };
        var hold = new LegalHold { Id = Guid.NewGuid(), MediaItemId = mediaId, Reason = "Active Investigation", CreatedBy = "admin" };
        var holds = new Dictionary<Guid, LegalHold> { { mediaId, hold } };
        var integrity = new List<IntegrityRecord>();

        // Act
        var result = EvidenceInspectorMapper.ToInspector(mediaItem, holds, integrity);

        // Assert
        Assert.NotNull(result.Provenance);
        var provenanceDict = result.Provenance.ToDictionary(x => x.Key, x => x.Value);
        Assert.Equal("On Hold", provenanceDict["LegalHoldStatus"]);
    }

    [Fact]
    public void ToInspector_MediaNotOnHold_LegalHoldStatusNoHold()
    {
        // Arrange
        var mediaId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var media = new MediaItem
        {
            Id = mediaId,
            DeviceId = deviceId,
            FileName = "video.mp4",
            FilePath = "/path",
            MediaFormat = "video/mp4",
            FileSizeBytes = 1000000,
            RecordedAtUtc = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc),
            DownloadedAtUtc = new DateTime(2024, 1, 15, 10, 35, 0, DateTimeKind.Utc),
            Sha256Hash = "xyz789",
            IntegrityVerified = false
        };
        var mediaItem = new EvidenceItem
        {
            Key = $"media:{mediaId}",
            Kind = EvidenceKind.Video,
            OccurredAtUtc = media.RecordedAtUtc,
            DeviceId = deviceId,
            DeviceName = "Driveway",
            Event = null,
            Media = media
        };
        var holds = new Dictionary<Guid, LegalHold>();
        var integrity = new List<IntegrityRecord>();

        // Act
        var result = EvidenceInspectorMapper.ToInspector(mediaItem, holds, integrity);

        // Assert
        Assert.NotNull(result.Provenance);
        var provenanceDict = result.Provenance.ToDictionary(x => x.Key, x => x.Value);
        Assert.Equal("No Hold", provenanceDict["LegalHoldStatus"]);
    }

    [Fact]
    public void ToInspector_EventItem_SetsPin_ToEventAndEventId()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var eventItem = new EvidenceItem
        {
            Key = "event:123",
            Kind = EvidenceKind.Event,
            OccurredAtUtc = DateTime.UtcNow,
            DeviceId = deviceId,
            DeviceName = "Front Door",
            Event = new Event { Id = eventId, EventType = "Motion Detected", ProviderEventId = "event:1" },
            Media = null
        };
        var holds = new Dictionary<Guid, LegalHold>();
        var integrity = new List<IntegrityRecord>();

        // Act
        var result = EvidenceInspectorMapper.ToInspector(eventItem, holds, integrity);

        // Assert
        Assert.NotNull(result.Pin);
        Assert.Equal(CaseItemKind.Event, result.Pin!.Kind);
        Assert.Equal(eventId, result.Pin.TargetId);
    }

    [Fact]
    public void ToInspector_EventItemWithMedia_StillPinsToEventNotMedia()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var mediaId = Guid.NewGuid();
        var media = new MediaItem
        {
            Id = mediaId,
            DeviceId = deviceId,
            DownloadEventId = eventId,
            FileName = "video.mp4",
            FilePath = "/path",
            MediaFormat = "video/mp4",
            FileSizeBytes = 1000,
            RecordedAtUtc = DateTime.UtcNow,
            DownloadedAtUtc = DateTime.UtcNow,
            Sha256Hash = "abc"
        };
        var eventItem = new EvidenceItem
        {
            Key = "event:123",
            Kind = EvidenceKind.Event,
            OccurredAtUtc = DateTime.UtcNow,
            DeviceId = deviceId,
            DeviceName = "Front Door",
            Event = new Event { Id = eventId, EventType = "Motion Detected", ProviderEventId = "event:1" },
            Media = media
        };
        var holds = new Dictionary<Guid, LegalHold>();
        var integrity = new List<IntegrityRecord>();

        // Act
        var result = EvidenceInspectorMapper.ToInspector(eventItem, holds, integrity);

        // Assert
        Assert.NotNull(result.Pin);
        Assert.Equal(CaseItemKind.Event, result.Pin!.Kind);
        Assert.Equal(eventId, result.Pin.TargetId);
    }

    [Fact]
    public void ToInspector_MediaOnlyItem_SetsPin_ToMediaAndMediaId()
    {
        // Arrange
        var deviceId = Guid.NewGuid();
        var mediaId = Guid.NewGuid();
        var media = new MediaItem
        {
            Id = mediaId,
            DeviceId = deviceId,
            FileName = "snapshot.jpg",
            FilePath = "/path",
            MediaFormat = "image/jpeg",
            FileSizeBytes = 123456,
            RecordedAtUtc = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc),
            DownloadedAtUtc = new DateTime(2024, 1, 15, 10, 35, 0, DateTimeKind.Utc),
            Sha256Hash = "abc123",
            IntegrityVerified = false
        };
        var mediaItem = new EvidenceItem
        {
            Key = $"media:{mediaId}",
            Kind = EvidenceKind.Snapshot,
            OccurredAtUtc = media.RecordedAtUtc,
            DeviceId = deviceId,
            DeviceName = "Doorbell",
            Event = null,
            Media = media
        };
        var holds = new Dictionary<Guid, LegalHold>();
        var integrity = new List<IntegrityRecord>();

        // Act
        var result = EvidenceInspectorMapper.ToInspector(mediaItem, holds, integrity);

        // Assert
        Assert.NotNull(result.Pin);
        Assert.Equal(CaseItemKind.Media, result.Pin!.Kind);
        Assert.Equal(mediaId, result.Pin.TargetId);
    }

    [Fact]
    public void ToInspector_SkipsNullProvenanceValues()
    {
        // Arrange
        var mediaId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var media = new MediaItem
        {
            Id = mediaId,
            DeviceId = deviceId,
            FileName = "file.bin",
            FilePath = "/path",
            MediaFormat = "application/octet-stream",
            FileSizeBytes = 100000,
            RecordedAtUtc = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc),
            DownloadedAtUtc = new DateTime(2024, 1, 15, 10, 35, 0, DateTimeKind.Utc),
            Sha256Hash = "abc123",
            IntegrityVerified = false,
            LastVerifiedAtUtc = null,
            ApiSourceHash = null
        };
        var mediaItem = new EvidenceItem
        {
            Key = $"media:{mediaId}",
            Kind = EvidenceKind.File,
            OccurredAtUtc = media.RecordedAtUtc,
            DeviceId = deviceId,
            DeviceName = "Device",
            Event = null,
            Media = media
        };
        var holds = new Dictionary<Guid, LegalHold>();
        var integrity = new List<IntegrityRecord>();

        // Act
        var result = EvidenceInspectorMapper.ToInspector(mediaItem, holds, integrity);

        // Assert
        Assert.NotNull(result.Provenance);
        var provenanceDict = result.Provenance.ToDictionary(x => x.Key, x => x.Value);

        // These should not be in provenance because they are null
        Assert.DoesNotContain("LastVerifiedAtUtc", provenanceDict.Keys);
        Assert.DoesNotContain("ApiSourceHash", provenanceDict.Keys);
    }
}
