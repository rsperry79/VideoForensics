namespace VideoForensics.Ui.Shared.Tests.Services.Evidence;

using Xunit;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Ui.Shared.Services.Evidence;

public class EvidenceItem_Label_Tests
{
    [Fact]
    public void Label_ForEventItem_ReturnsEventType()
    {
        // Arrange
        var evt = new Event
        {
            Id = Guid.NewGuid(),
            EventType = "Motion Detected",
            OccurredAtUtc = DateTime.UtcNow,
            DiscoveredAtUtc = DateTime.UtcNow,
            ProviderEventId = "test",
            MetadataJson = "{}"
        };

        var item = new EvidenceItem
        {
            Key = "event:test",
            Kind = EvidenceKind.Event,
            OccurredAtUtc = DateTime.UtcNow,
            DeviceId = Guid.NewGuid(),
            DeviceName = "Front Door",
            Event = evt,
            Media = null
        };

        // Act
        var label = item.Label;

        // Assert
        Assert.Equal("Motion Detected", label);
    }

    [Fact]
    public void Label_ForSnapshot_ReturnsSnapshot()
    {
        // Arrange
        var media = new MediaItem
        {
            Id = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            FileName = "snapshot.jpg",
            FilePath = "/path/snapshot.jpg",
            MediaFormat = "image/jpeg",
            Sha256Hash = "hash",
            RecordedAtUtc = DateTime.UtcNow,
            DownloadedAtUtc = DateTime.UtcNow
        };

        var item = new EvidenceItem
        {
            Key = "media:test",
            Kind = EvidenceKind.Snapshot,
            OccurredAtUtc = DateTime.UtcNow,
            DeviceId = Guid.NewGuid(),
            DeviceName = "Front Door",
            Event = null,
            Media = media
        };

        // Act
        var label = item.Label;

        // Assert
        Assert.Equal("Snapshot", label);
    }

    [Fact]
    public void Label_ForVideo_ReturnsVideo()
    {
        // Arrange
        var item = new EvidenceItem
        {
            Key = "media:test",
            Kind = EvidenceKind.Video,
            OccurredAtUtc = DateTime.UtcNow,
            DeviceId = Guid.NewGuid(),
            DeviceName = "Front Door",
            Event = null,
            Media = null
        };

        // Act
        var label = item.Label;

        // Assert
        Assert.Equal("Video", label);
    }

    [Fact]
    public void Label_ForFile_ReturnsFile()
    {
        // Arrange
        var item = new EvidenceItem
        {
            Key = "media:test",
            Kind = EvidenceKind.File,
            OccurredAtUtc = DateTime.UtcNow,
            DeviceId = Guid.NewGuid(),
            DeviceName = "Front Door",
            Event = null,
            Media = null
        };

        // Act
        var label = item.Label;

        // Assert
        Assert.Equal("File", label);
    }
}

public class EvidenceItem_HasViewableMedia_Tests
{
    [Fact]
    public void HasViewableMedia_WithImageMedia_ReturnsTrue()
    {
        // Arrange
        var media = new MediaItem
        {
            Id = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            FileName = "snapshot.jpg",
            FilePath = "/path/snapshot.jpg",
            MediaFormat = "image/jpeg",
            Sha256Hash = "hash",
            RecordedAtUtc = DateTime.UtcNow,
            DownloadedAtUtc = DateTime.UtcNow
        };

        var item = new EvidenceItem
        {
            Key = "media:test",
            Kind = EvidenceKind.Snapshot,
            OccurredAtUtc = DateTime.UtcNow,
            DeviceId = Guid.NewGuid(),
            DeviceName = "Front Door",
            Event = null,
            Media = media
        };

        // Act
        var hasViewable = item.HasViewableMedia;

        // Assert
        Assert.True(hasViewable);
    }

    [Fact]
    public void HasViewableMedia_WithVideoMedia_ReturnsTrue()
    {
        // Arrange
        var media = new MediaItem
        {
            Id = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            FileName = "video.mp4",
            FilePath = "/path/video.mp4",
            MediaFormat = "video/mp4",
            Sha256Hash = "hash",
            RecordedAtUtc = DateTime.UtcNow,
            DownloadedAtUtc = DateTime.UtcNow
        };

        var item = new EvidenceItem
        {
            Key = "media:test",
            Kind = EvidenceKind.Video,
            OccurredAtUtc = DateTime.UtcNow,
            DeviceId = Guid.NewGuid(),
            DeviceName = "Front Door",
            Event = null,
            Media = media
        };

        // Act
        var hasViewable = item.HasViewableMedia;

        // Assert
        Assert.True(hasViewable);
    }

    [Fact]
    public void HasViewableMedia_WithNoMedia_ReturnsFalse()
    {
        // Arrange
        var item = new EvidenceItem
        {
            Key = "event:test",
            Kind = EvidenceKind.Event,
            OccurredAtUtc = DateTime.UtcNow,
            DeviceId = Guid.NewGuid(),
            DeviceName = "Front Door",
            Event = null,
            Media = null
        };

        // Act
        var hasViewable = item.HasViewableMedia;

        // Assert
        Assert.False(hasViewable);
    }

    [Fact]
    public void HasViewableMedia_WithNonViewableMedia_ReturnsFalse()
    {
        // Arrange
        var media = new MediaItem
        {
            Id = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            FileName = "data.dat",
            FilePath = "/path/data.dat",
            MediaFormat = "application/octet-stream",
            Sha256Hash = "hash",
            RecordedAtUtc = DateTime.UtcNow,
            DownloadedAtUtc = DateTime.UtcNow
        };

        var item = new EvidenceItem
        {
            Key = "media:test",
            Kind = EvidenceKind.File,
            OccurredAtUtc = DateTime.UtcNow,
            DeviceId = Guid.NewGuid(),
            DeviceName = "Front Door",
            Event = null,
            Media = media
        };

        // Act
        var hasViewable = item.HasViewableMedia;

        // Assert
        Assert.False(hasViewable);
    }
}

public class EvidenceItem_NonViewableMediaClassification_Tests
{
    [Fact]
    public void Kind_WithOctetStreamFormat_IsFileAndNotViewable()
    {
        // Arrange
        var media = new MediaItem
        {
            Id = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            FileName = "data.dat",
            FilePath = "/path/data.dat",
            MediaFormat = "application/octet-stream",
            Sha256Hash = "hash",
            RecordedAtUtc = DateTime.UtcNow,
            DownloadedAtUtc = DateTime.UtcNow
        };

        var item = new EvidenceItem
        {
            Key = "media:test",
            Kind = EvidenceKind.File,
            OccurredAtUtc = DateTime.UtcNow,
            DeviceId = Guid.NewGuid(),
            DeviceName = "Front Door",
            Event = null,
            Media = media
        };

        // Act & Assert
        Assert.Equal(EvidenceKind.File, item.Kind);
        Assert.False(item.HasViewableMedia);
    }
}
