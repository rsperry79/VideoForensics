namespace VideoForensics.Ui.Shared.Tests;

using Xunit;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Core.Models;
using VideoForensics.Ui.Shared.Services.Cases;

public class CaseItemIntegrity_Tests
{
    [Fact]
    public void Compare_WithEventItem_ReturnsNotApplicable()
    {
        // Arrange
        var eventItem = new CaseItem
        {
            Id = Guid.NewGuid(),
            Kind = CaseItemKind.Event,
            EventId = Guid.NewGuid(),
            Reason = "Test event",
            AddedBy = "test"
        };

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
        var result = CaseItemIntegrity.Compare(eventItem, currentMedia);

        // Assert
        Assert.Equal(PinIntegrity.NotApplicable, result);
    }

    [Fact]
    public void Compare_WithMediaItemAndNullCurrentMedia_ReturnsMediaMissing()
    {
        // Arrange
        var mediaItem = new CaseItem
        {
            Id = Guid.NewGuid(),
            Kind = CaseItemKind.Media,
            MediaItemId = Guid.NewGuid(),
            MediaSha256AtAdd = "abc123",
            Reason = "Test media",
            AddedBy = "test"
        };

        // Act
        var result = CaseItemIntegrity.Compare(mediaItem, null);

        // Assert
        Assert.Equal(PinIntegrity.MediaMissing, result);
    }

    [Fact]
    public void Compare_WithMatchingHash_ReturnsMatch()
    {
        // Arrange
        var hash = "ABC123DEF456";
        var mediaItem = new CaseItem
        {
            Id = Guid.NewGuid(),
            Kind = CaseItemKind.Media,
            MediaItemId = Guid.NewGuid(),
            MediaSha256AtAdd = hash,
            Reason = "Test media",
            AddedBy = "test"
        };

        var currentMedia = new MediaItem
        {
            Id = mediaItem.MediaItemId.Value,
            DeviceId = Guid.NewGuid(),
            FileName = "test.mp4",
            FilePath = "/path/to/test.mp4",
            MediaFormat = "mp4",
            Sha256Hash = hash.ToLower(),  // Case-insensitive comparison
            RecordedAtUtc = DateTime.UtcNow,
            DownloadedAtUtc = DateTime.UtcNow
        };

        // Act
        var result = CaseItemIntegrity.Compare(mediaItem, currentMedia);

        // Assert
        Assert.Equal(PinIntegrity.Match, result);
    }

    [Fact]
    public void Compare_WithMismatchedHash_ReturnsMismatch()
    {
        // Arrange
        var mediaItem = new CaseItem
        {
            Id = Guid.NewGuid(),
            Kind = CaseItemKind.Media,
            MediaItemId = Guid.NewGuid(),
            MediaSha256AtAdd = "ABC123",
            Reason = "Test media",
            AddedBy = "test"
        };

        var currentMedia = new MediaItem
        {
            Id = mediaItem.MediaItemId.Value,
            DeviceId = Guid.NewGuid(),
            FileName = "test.mp4",
            FilePath = "/path/to/test.mp4",
            MediaFormat = "mp4",
            Sha256Hash = "XYZ789",
            RecordedAtUtc = DateTime.UtcNow,
            DownloadedAtUtc = DateTime.UtcNow
        };

        // Act
        var result = CaseItemIntegrity.Compare(mediaItem, currentMedia);

        // Assert
        Assert.Equal(PinIntegrity.Mismatch, result);
    }

    [Fact]
    public void Compare_WithHashCaseInsensitivity_ReturnsMatch()
    {
        // Arrange
        var mediaItem = new CaseItem
        {
            Id = Guid.NewGuid(),
            Kind = CaseItemKind.Media,
            MediaItemId = Guid.NewGuid(),
            MediaSha256AtAdd = "aBc123DeF456",
            Reason = "Test media",
            AddedBy = "test"
        };

        var currentMedia = new MediaItem
        {
            Id = mediaItem.MediaItemId.Value,
            DeviceId = Guid.NewGuid(),
            FileName = "test.mp4",
            FilePath = "/path/to/test.mp4",
            MediaFormat = "mp4",
            Sha256Hash = "ABC123DEF456",  // Different case
            RecordedAtUtc = DateTime.UtcNow,
            DownloadedAtUtc = DateTime.UtcNow
        };

        // Act
        var result = CaseItemIntegrity.Compare(mediaItem, currentMedia);

        // Assert
        Assert.Equal(PinIntegrity.Match, result);
    }
}
