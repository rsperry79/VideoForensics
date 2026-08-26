using VideoForensics.Data.Common.Entities;
using Xunit;

namespace VideoForensics.Data.Common.Tests;

public class ReconciliationAndExportEntityTests
{
    [Fact]
    public void ProviderReconciliationRecord_PropertiesRoundTrip_ReturnsSetValues()
    {
        // Arrange
        var id = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var ranAtUtc = DateTime.UtcNow;
        var providerEventId = "event-999";
        var discrepancyType = DiscrepancyType.MetadataChanged;
        var fieldName = "EventType";
        var storedValue = "motion";
        var providerValue = "doorbell";
        var notes = "Event type changed on provider side";

        // Act
        var record = new ProviderReconciliationRecord
        {
            Id = id,
            DeviceId = deviceId,
            RanAtUtc = ranAtUtc,
            ProviderEventId = providerEventId,
            DiscrepancyType = discrepancyType,
            FieldName = fieldName,
            StoredValue = storedValue,
            ProviderValue = providerValue,
            Notes = notes
        };

        // Assert
        Assert.Equal(id, record.Id);
        Assert.Equal(deviceId, record.DeviceId);
        Assert.Equal(ranAtUtc, record.RanAtUtc);
        Assert.Equal(providerEventId, record.ProviderEventId);
        Assert.Equal(DiscrepancyType.MetadataChanged, record.DiscrepancyType);
        Assert.Equal(fieldName, record.FieldName);
        Assert.Equal(storedValue, record.StoredValue);
        Assert.Equal(providerValue, record.ProviderValue);
        Assert.Equal(notes, record.Notes);
    }

    [Fact]
    public void ProviderReconciliationRecord_MissingFromProvider_RoundsTrip()
    {
        // Arrange & Act
        var record = new ProviderReconciliationRecord
        {
            Id = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            RanAtUtc = DateTime.UtcNow,
            ProviderEventId = "event-999",
            DiscrepancyType = DiscrepancyType.MissingFromProvider,
            FieldName = null,
            StoredValue = null,
            ProviderValue = null,
            Notes = "Event exists in local DB but not on provider"
        };

        // Assert
        Assert.Equal(DiscrepancyType.MissingFromProvider, record.DiscrepancyType);
        Assert.Null(record.FieldName);
        Assert.Null(record.StoredValue);
        Assert.Null(record.ProviderValue);
    }

    [Fact]
    public void ProviderReconciliationRecord_NewEventOnProvider_RoundsTrip()
    {
        // Arrange & Act
        var record = new ProviderReconciliationRecord
        {
            Id = Guid.NewGuid(),
            DeviceId = Guid.NewGuid(),
            RanAtUtc = DateTime.UtcNow,
            ProviderEventId = "event-new",
            DiscrepancyType = DiscrepancyType.NewEventFoundOnProvider,
            FieldName = null,
            StoredValue = null,
            ProviderValue = null,
            Notes = "New event found on provider not yet in local DB"
        };

        // Assert
        Assert.Equal(DiscrepancyType.NewEventFoundOnProvider, record.DiscrepancyType);
    }

    [Fact]
    public void ReconciliationDiscrepancy_PropertiesRoundTrip_ReturnsSetValues()
    {
        // Arrange
        var type = DiscrepancyType.MetadataChanged;
        var providerEventId = "event-123";
        var fieldName = "Timestamp";
        var storedValue = "2024-01-15T10:00:00Z";
        var providerValue = "2024-01-15T10:05:00Z";

        // Act
        var discrepancy = new ReconciliationDiscrepancy
        {
            Type = type,
            ProviderEventId = providerEventId,
            FieldName = fieldName,
            StoredValue = storedValue,
            ProviderValue = providerValue
        };

        // Assert
        Assert.Equal(type, discrepancy.Type);
        Assert.Equal(providerEventId, discrepancy.ProviderEventId);
        Assert.Equal(fieldName, discrepancy.FieldName);
        Assert.Equal(storedValue, discrepancy.StoredValue);
        Assert.Equal(providerValue, discrepancy.ProviderValue);
    }

    [Fact]
    public void ReconciliationDiscrepancy_WithoutOptionalFields_ReturnsNulls()
    {
        // Arrange & Act
        var discrepancy = new ReconciliationDiscrepancy
        {
            Type = DiscrepancyType.MissingFromProvider,
            ProviderEventId = "event-123",
            FieldName = null,
            StoredValue = null,
            ProviderValue = null
        };

        // Assert
        Assert.Null(discrepancy.FieldName);
        Assert.Null(discrepancy.StoredValue);
        Assert.Null(discrepancy.ProviderValue);
    }

    [Fact]
    public void ExportRecord_PropertiesRoundTrip_ReturnsSetValues()
    {
        // Arrange
        var id = Guid.NewGuid();
        var exportedAtUtc = DateTime.UtcNow;
        var exportedByUserName = "operator";
        var caseReference = "CASE-2024-001";
        var recipientDescription = "Law Enforcement Agency";
        var archiveFileName = "evidence_export_2024-01-15.zip";
        var archiveSha256Hash = "abcdef123456789";
        var wasEncrypted = true;
        var itemCount = 42;
        var appVersion = "1.0.0";

        // Act
        var record = new ExportRecord
        {
            Id = id,
            ExportedAtUtc = exportedAtUtc,
            ExportedByUserName = exportedByUserName,
            CaseReference = caseReference,
            RecipientDescription = recipientDescription,
            ArchiveFileName = archiveFileName,
            ArchiveSha256Hash = archiveSha256Hash,
            WasEncrypted = wasEncrypted,
            ItemCount = itemCount,
            AppVersion = appVersion
        };

        // Assert
        Assert.Equal(id, record.Id);
        Assert.Equal(exportedAtUtc, record.ExportedAtUtc);
        Assert.Equal(exportedByUserName, record.ExportedByUserName);
        Assert.Equal(caseReference, record.CaseReference);
        Assert.Equal(recipientDescription, record.RecipientDescription);
        Assert.Equal(archiveFileName, record.ArchiveFileName);
        Assert.Equal(archiveSha256Hash, record.ArchiveSha256Hash);
        Assert.True(record.WasEncrypted);
        Assert.Equal(itemCount, record.ItemCount);
        Assert.Equal(appVersion, record.AppVersion);
    }

    [Fact]
    public void ExportRecord_WithoutOptionalMetadata_ReturnsNulls()
    {
        // Arrange & Act
        var record = new ExportRecord
        {
            Id = Guid.NewGuid(),
            ExportedAtUtc = DateTime.UtcNow,
            ExportedByUserName = "operator",
            CaseReference = null,
            RecipientDescription = null,
            ArchiveFileName = "export.zip",
            ArchiveSha256Hash = "hash123",
            WasEncrypted = false,
            ItemCount = 5,
            AppVersion = "1.0.0"
        };

        // Assert
        Assert.Null(record.CaseReference);
        Assert.Null(record.RecipientDescription);
        Assert.False(record.WasEncrypted);
    }

    [Fact]
    public void ExportRecord_UnencryptedExport_RoundsTrip()
    {
        // Arrange & Act
        var record = new ExportRecord
        {
            Id = Guid.NewGuid(),
            ExportedAtUtc = DateTime.UtcNow,
            ExportedByUserName = "operator",
            CaseReference = "CASE-2024-002",
            RecipientDescription = "Internal Investigation",
            ArchiveFileName = "internal_export.zip",
            ArchiveSha256Hash = "unencrypted_hash",
            WasEncrypted = false,
            ItemCount = 15,
            AppVersion = "1.0.0"
        };

        // Assert
        Assert.False(record.WasEncrypted);
        Assert.NotNull(record.CaseReference);
    }

    [Fact]
    public void ExportRecordItem_PropertiesRoundTrip_ReturnsSetValues()
    {
        // Arrange
        var id = Guid.NewGuid();
        var exportRecordId = Guid.NewGuid();
        var mediaItemId = Guid.NewGuid();
        var mediaItemSha256HashAtExport = "media_hash_at_export_time";

        // Act
        var item = new ExportRecordItem
        {
            Id = id,
            ExportRecordId = exportRecordId,
            MediaItemId = mediaItemId,
            MediaItemSha256HashAtExport = mediaItemSha256HashAtExport
        };

        // Assert
        Assert.Equal(id, item.Id);
        Assert.Equal(exportRecordId, item.ExportRecordId);
        Assert.Equal(mediaItemId, item.MediaItemId);
        Assert.Equal(mediaItemSha256HashAtExport, item.MediaItemSha256HashAtExport);
    }

    [Fact]
    public void ExportRecordItem_CapturesHashAtExportTime_NotCurrentHash()
    {
        // Arrange
        var mediaItemId = Guid.NewGuid();
        var hashAtExportTime = "hash_at_export";
        var currentHash = "different_hash_after_modification";

        // Act
        var item = new ExportRecordItem
        {
            Id = Guid.NewGuid(),
            ExportRecordId = Guid.NewGuid(),
            MediaItemId = mediaItemId,
            MediaItemSha256HashAtExport = hashAtExportTime
        };

        // Assert
        Assert.Equal(hashAtExportTime, item.MediaItemSha256HashAtExport);
        Assert.NotEqual(currentHash, item.MediaItemSha256HashAtExport);
    }

    [Fact]
    public void ExportRecordItem_MultipleItemsPerExport_PreservesIndividualHashes()
    {
        // Arrange
        var exportRecordId = Guid.NewGuid();
        var mediaItemId1 = Guid.NewGuid();
        var mediaItemId2 = Guid.NewGuid();
        var hash1 = "item1_hash_at_export";
        var hash2 = "item2_hash_at_export";

        // Act
        var item1 = new ExportRecordItem
        {
            Id = Guid.NewGuid(),
            ExportRecordId = exportRecordId,
            MediaItemId = mediaItemId1,
            MediaItemSha256HashAtExport = hash1
        };

        var item2 = new ExportRecordItem
        {
            Id = Guid.NewGuid(),
            ExportRecordId = exportRecordId,
            MediaItemId = mediaItemId2,
            MediaItemSha256HashAtExport = hash2
        };

        // Assert
        Assert.Equal(exportRecordId, item1.ExportRecordId);
        Assert.Equal(exportRecordId, item2.ExportRecordId);
        Assert.NotEqual(item1.MediaItemId, item2.MediaItemId);
        Assert.NotEqual(item1.MediaItemSha256HashAtExport, item2.MediaItemSha256HashAtExport);
    }
}
