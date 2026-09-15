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
        string actor = "username";
        ActorType actorType = ActorType.Human;
        string action = "MediaDownloaded";
        string entityType = "MediaItem";
        var entityId = Guid.NewGuid();
        string detailsJson = "{\"fileSize\": 1024000}";
        DateTime timestampUtc = DateTime.UtcNow;
        string previousEntryHash = "prev_hash_value";
        string entryHash = "hash_value";

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
        string sha256Hash = "abcdef123456";
        DateTime verifiedAtUtc = DateTime.UtcNow;
        bool passed = true;
        string verifiedBy = "operator";

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
        string failureReason = "Hash mismatch: file has been tampered with";

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
        DateTime firstVerification = DateTime.UtcNow.AddDays(-1);
        DateTime secondVerification = DateTime.UtcNow;

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
}
