using VideoForensics.Data.Common.Entities;
using Xunit;

namespace VideoForensics.Data.Common.Tests;

public class AuditEntityTests
{
    [Fact]
    public void ActionLogEntry_PropertiesRoundTrip_ReturnsSetValues()
    {
        // Arrange
        var id = Guid.NewGuid();
        var actor = "username";
        var actorType = ActorType.Human;
        var action = "MediaDownloaded";
        var entityType = "MediaItem";
        var entityId = Guid.NewGuid();
        var detailsJson = "{\"fileSize\": 1024000}";
        var timestampUtc = DateTime.UtcNow;
        var previousEntryHash = "prev_hash_value";
        var entryHash = "hash_value";

        // Act
        var logEntry = new ActionLogEntry
        {
            Id = id,
            Actor = actor,
            ActorType = actorType,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            DetailsJson = detailsJson,
            TimestampUtc = timestampUtc,
            PreviousEntryHash = previousEntryHash,
            EntryHash = entryHash
        };

        // Assert
        Assert.Equal(id, logEntry.Id);
        Assert.Equal(actor, logEntry.Actor);
        Assert.Equal(actorType, logEntry.ActorType);
        Assert.Equal(action, logEntry.Action);
        Assert.Equal(entityType, logEntry.EntityType);
        Assert.Equal(entityId, logEntry.EntityId);
        Assert.Equal(detailsJson, logEntry.DetailsJson);
        Assert.Equal(timestampUtc, logEntry.TimestampUtc);
        Assert.Equal(previousEntryHash, logEntry.PreviousEntryHash);
        Assert.Equal(entryHash, logEntry.EntryHash);
    }

    [Fact]
    public void ActionLogEntry_FirstEntryInChain_PreviousEntryHashIsNull()
    {
        // Arrange & Act
        var logEntry = new ActionLogEntry
        {
            Id = Guid.NewGuid(),
            Actor = "username",
            ActorType = ActorType.Human,
            Action = "Initialized",
            EntityType = "Database",
            EntityId = null,
            DetailsJson = null,
            TimestampUtc = DateTime.UtcNow,
            PreviousEntryHash = null,
            EntryHash = "hash_value"
        };

        // Assert
        Assert.Null(logEntry.PreviousEntryHash);
        Assert.Null(logEntry.EntityId);
        Assert.Null(logEntry.DetailsJson);
    }

    [Fact]
    public void ActionLogEntry_WithSystemActorType_RoundsTrip()
    {
        // Arrange & Act
        var logEntry = new ActionLogEntry
        {
            Id = Guid.NewGuid(),
            Actor = "system",
            ActorType = ActorType.System,
            Action = "RetentionPolicyExecuted",
            EntityType = "MediaItem",
            EntityId = Guid.NewGuid(),
            DetailsJson = "{\"purgedCount\": 10}",
            TimestampUtc = DateTime.UtcNow,
            PreviousEntryHash = "prev_hash",
            EntryHash = "hash_value"
        };

        // Assert
        Assert.Equal(ActorType.System, logEntry.ActorType);
        Assert.Equal("system", logEntry.Actor);
    }

    [Fact]
    public void ActionLogEntry_WithMcpToolActorType_RoundsTrip()
    {
        // Arrange & Act
        var logEntry = new ActionLogEntry
        {
            Id = Guid.NewGuid(),
            Actor = "mcp:face-recognition",
            ActorType = ActorType.McpTool,
            Action = "AnnotationAdded",
            EntityType = "MediaItem",
            EntityId = Guid.NewGuid(),
            DetailsJson = "{\"recognizedPerson\": \"John Doe\"}",
            TimestampUtc = DateTime.UtcNow,
            PreviousEntryHash = "prev_hash",
            EntryHash = "hash_value"
        };

        // Assert
        Assert.Equal(ActorType.McpTool, logEntry.ActorType);
        Assert.StartsWith("mcp:", logEntry.Actor);
    }

    [Fact]
    public void IntegrityRecord_PropertiesRoundTrip_ReturnsSetValues()
    {
        // Arrange
        var id = Guid.NewGuid();
        var mediaItemId = Guid.NewGuid();
        var sha256Hash = "abcdef123456";
        var verifiedAtUtc = DateTime.UtcNow;
        var passed = true;
        var verifiedBy = "operator";

        // Act
        var record = new IntegrityRecord
        {
            Id = id,
            MediaItemId = mediaItemId,
            Sha256Hash = sha256Hash,
            VerifiedAtUtc = verifiedAtUtc,
            Passed = passed,
            FailureReason = null,
            VerifiedBy = verifiedBy
        };

        // Assert
        Assert.Equal(id, record.Id);
        Assert.Equal(mediaItemId, record.MediaItemId);
        Assert.Equal(sha256Hash, record.Sha256Hash);
        Assert.Equal(verifiedAtUtc, record.VerifiedAtUtc);
        Assert.True(record.Passed);
        Assert.Null(record.FailureReason);
        Assert.Equal(verifiedBy, record.VerifiedBy);
    }

    [Fact]
    public void IntegrityRecord_WhenVerificationFails_ContainsFailureReason()
    {
        // Arrange
        var failureReason = "Hash mismatch: file has been tampered with";

        // Act
        var record = new IntegrityRecord
        {
            Id = Guid.NewGuid(),
            MediaItemId = Guid.NewGuid(),
            Sha256Hash = "abcdef123456",
            VerifiedAtUtc = DateTime.UtcNow,
            Passed = false,
            FailureReason = failureReason,
            VerifiedBy = "automated_verification"
        };

        // Assert
        Assert.False(record.Passed);
        Assert.Equal(failureReason, record.FailureReason);
    }

    [Fact]
    public void IntegrityRecord_MultipleVerificationsPerItem_RoundsTrip()
    {
        // Arrange
        var mediaItemId = Guid.NewGuid();
        var firstVerification = DateTime.UtcNow.AddDays(-1);
        var secondVerification = DateTime.UtcNow;

        // Act
        var record1 = new IntegrityRecord
        {
            Id = Guid.NewGuid(),
            MediaItemId = mediaItemId,
            Sha256Hash = "hash123",
            VerifiedAtUtc = firstVerification,
            Passed = true,
            FailureReason = null,
            VerifiedBy = "operator"
        };

        var record2 = new IntegrityRecord
        {
            Id = Guid.NewGuid(),
            MediaItemId = mediaItemId,
            Sha256Hash = "hash123",
            VerifiedAtUtc = secondVerification,
            Passed = true,
            FailureReason = null,
            VerifiedBy = "automated"
        };

        // Assert
        Assert.Equal(mediaItemId, record1.MediaItemId);
        Assert.Equal(mediaItemId, record2.MediaItemId);
        Assert.True(record1.VerifiedAtUtc < record2.VerifiedAtUtc);
        Assert.Equal(record1.Sha256Hash, record2.Sha256Hash);
    }

    [Fact]
    public void Annotation_PropertiesRoundTrip_ReturnsSetValues()
    {
        // Arrange
        var id = Guid.NewGuid();
        var entityType = "MediaItem";
        var entityId = Guid.NewGuid();
        var source = "mcp:face-recognition";
        var key = "recognized_person";
        var value = "Jane Doe";
        var createdAtUtc = DateTime.UtcNow;

        // Act
        var annotation = new Annotation
        {
            Id = id,
            EntityType = entityType,
            EntityId = entityId,
            Source = source,
            Key = key,
            Value = value,
            CreatedAtUtc = createdAtUtc
        };

        // Assert
        Assert.Equal(id, annotation.Id);
        Assert.Equal(entityType, annotation.EntityType);
        Assert.Equal(entityId, annotation.EntityId);
        Assert.Equal(source, annotation.Source);
        Assert.Equal(key, annotation.Key);
        Assert.Equal(value, annotation.Value);
        Assert.Equal(createdAtUtc, annotation.CreatedAtUtc);
    }

    [Fact]
    public void Annotation_OnEvent_RoundsTrip()
    {
        // Arrange & Act
        var annotation = new Annotation
        {
            Id = Guid.NewGuid(),
            EntityType = "Event",
            EntityId = Guid.NewGuid(),
            Source = "mcp:anomaly-detector",
            Key = "anomaly_score",
            Value = "0.85",
            CreatedAtUtc = DateTime.UtcNow
        };

        // Assert
        Assert.Equal("Event", annotation.EntityType);
        Assert.Equal("anomaly_score", annotation.Key);
    }

    [Fact]
    public void Annotation_MultipleAnnotationsPerEntity_Distinguishes()
    {
        // Arrange
        var entityId = Guid.NewGuid();
        var createdTime = DateTime.UtcNow;

        // Act
        var annotation1 = new Annotation
        {
            Id = Guid.NewGuid(),
            EntityType = "MediaItem",
            EntityId = entityId,
            Source = "mcp:face-recognition",
            Key = "face_name_1",
            Value = "John Doe",
            CreatedAtUtc = createdTime
        };

        var annotation2 = new Annotation
        {
            Id = Guid.NewGuid(),
            EntityType = "MediaItem",
            EntityId = entityId,
            Source = "mcp:face-recognition",
            Key = "face_name_2",
            Value = "Jane Smith",
            CreatedAtUtc = createdTime.AddMilliseconds(100)
        };

        // Assert
        Assert.Equal(entityId, annotation1.EntityId);
        Assert.Equal(entityId, annotation2.EntityId);
        Assert.NotEqual(annotation1.Key, annotation2.Key);
        Assert.NotEqual(annotation1.Value, annotation2.Value);
    }
}
