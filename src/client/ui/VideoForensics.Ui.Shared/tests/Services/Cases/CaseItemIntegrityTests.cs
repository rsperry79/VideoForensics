using VideoForensics.Client.Ui.Shared.Services.Cases;using VideoForensics.Core.Api.Contracts;
using VideoForensics.Data.Common.Entities;

using Xunit;

namespace VideoForensics.Client.Ui.Shared.Tests.Services.Cases;

public class CaseItemIntegrity_Tests
{
    [Fact]
    public void Compare_WithSnapshotItem_ReturnsNotApplicable()
    {
        var snapshotItem = new CaseItemDto(Guid.NewGuid(), Guid.NewGuid(), CaseItemKindDto.Snapshot.ToString(), Guid.NewGuid(), "Test snapshot", "test", DateTime.UtcNow, null, null, null, null);

        PinIntegrity result = CaseItemIntegrity.Compare(snapshotItem, null);

        Assert.Equal(PinIntegrity.NotApplicable, result);
    }

    [Fact]
    public void Compare_WithEventItem_ReturnsNotApplicable()
    {
        // Arrange
        var eventItem = new CaseItemDto(Guid.NewGuid(), Guid.NewGuid(), CaseItemKindDto.Event.ToString(), Guid.NewGuid(), "Test event", "test", DateTime.UtcNow, null, null, null, null);

        var currentMedia = new MediaItem
        {
            Id = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            FileName = "test.mp4",
            FilePath = "/path/to/test.mp4",
            MediaFormat = "mp4",
            Sha256Hash = "abc123",
            RecordedAtUtc = DateTime.UtcNow,
            DownloadedAtUtc = DateTime.UtcNow
        };

        // Act
        PinIntegrity result = CaseItemIntegrity.Compare(eventItem, currentMedia?.ToDto());

        // Assert
        Assert.Equal(PinIntegrity.NotApplicable, result);
    }

    [Fact]
    public void Compare_WithMediaItemAndNullCurrentMedia_ReturnsMediaMissing()
    {
        // Arrange
        var mediaItem = new CaseItemDto(Guid.NewGuid(), Guid.NewGuid(), CaseItemKindDto.Media.ToString(), Guid.NewGuid(), "Test media", "test", DateTime.UtcNow, "abc123", null, null, null);

        // Act
        PinIntegrity result = CaseItemIntegrity.Compare(mediaItem, null);

        // Assert
        Assert.Equal(PinIntegrity.MediaMissing, result);
    }

    [Fact]
    public void Compare_WithMatchingHash_ReturnsMatch()
    {
        // Arrange
        string hash = "ABC123DEF456";
        var mediaItem = new CaseItemDto(Guid.NewGuid(), Guid.NewGuid(), CaseItemKindDto.Media.ToString(), Guid.NewGuid(), "Test media", "test", DateTime.UtcNow, hash, null, null, null);

        var currentMedia = new MediaItem
        {
            Id = mediaItem.TargetId,
            DeviceId = Guid.NewGuid(),
            FileName = "test.mp4",
            FilePath = "/path/to/test.mp4",
            MediaFormat = "mp4",
            Sha256Hash = hash.ToLower(),  // Case-insensitive comparison
            RecordedAtUtc = DateTime.UtcNow,
            DownloadedAtUtc = DateTime.UtcNow
        };

        // Act
        PinIntegrity result = CaseItemIntegrity.Compare(mediaItem, currentMedia?.ToDto());

        // Assert
        Assert.Equal(PinIntegrity.Match, result);
    }

    [Fact]
    public void Compare_WithMismatchedHash_ReturnsMismatch()
    {
        // Arrange
        var mediaItem = new CaseItemDto(Guid.NewGuid(), Guid.NewGuid(), CaseItemKindDto.Media.ToString(), Guid.NewGuid(), "Test media", "test", DateTime.UtcNow, "ABC123", null, null, null);

        var currentMedia = new MediaItem
        {
            Id = mediaItem.TargetId,
            DeviceId = Guid.NewGuid(),
            FileName = "test.mp4",
            FilePath = "/path/to/test.mp4",
            MediaFormat = "mp4",
            Sha256Hash = "XYZ789",
            RecordedAtUtc = DateTime.UtcNow,
            DownloadedAtUtc = DateTime.UtcNow
        };

        // Act
        PinIntegrity result = CaseItemIntegrity.Compare(mediaItem, currentMedia?.ToDto());

        // Assert
        Assert.Equal(PinIntegrity.Mismatch, result);
    }

    [Fact]
    public void Compare_WithHashCaseInsensitivity_ReturnsMatch()
    {
        // Arrange
        var mediaItem = new CaseItemDto(Guid.NewGuid(), Guid.NewGuid(), CaseItemKindDto.Media.ToString(), Guid.NewGuid(), "Test media", "test", DateTime.UtcNow, "aBc123DeF456", null, null, null);

        var currentMedia = new MediaItem
        {
            Id = mediaItem.TargetId,
            DeviceId = Guid.NewGuid(),
            FileName = "test.mp4",
            FilePath = "/path/to/test.mp4",
            MediaFormat = "mp4",
            Sha256Hash = "ABC123DEF456",  // Different case
            RecordedAtUtc = DateTime.UtcNow,
            DownloadedAtUtc = DateTime.UtcNow
        };

        // Act
        PinIntegrity result = CaseItemIntegrity.Compare(mediaItem, currentMedia?.ToDto());

        // Assert
        Assert.Equal(PinIntegrity.Match, result);
    }
}
