using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Core.Tests
{
    internal static class TestHelpers
    {
        /// <summary>Creates an ActionLogEntry with all required fields set to sensible defaults.</summary>
        public static ActionLogEntry CreateActionLogEntry(
            string? actor = null,
            string? action = null,
            string? entityType = null,
            string? entryHash = null,
            Guid? entityId = null,
            string? details = null)
        {
            return new ActionLogEntry
            {
                Id = Guid.NewGuid(),
                Actor = actor ?? "test_user",
                ActorType = ActorType.Human,
                Action = action ?? "TestAction",
                EntityType = entityType ?? "TestEntity",
                EntityId = entityId,
                DetailsJson = details,
                TimestampUtc = DateTime.UtcNow,
                PreviousEntryHash = null,
                EntryHash = entryHash ?? "test_hash_" + Guid.NewGuid().ToString("N")[..8]
            };
        }

        /// <summary>Creates an ExportRecord with all required fields set to sensible defaults.</summary>
        public static ExportRecord CreateExportRecord(
            string? exportedByUserName = null,
            string? archiveFileName = null,
            string? archiveSha256Hash = null,
            string? appVersion = null,
            string? caseReference = null,
            string? recipientDescription = null,
            int itemCount = 0)
        {
            return new ExportRecord
            {
                Id = Guid.NewGuid(),
                ExportedAtUtc = DateTime.UtcNow,
                ExportedByUserName = exportedByUserName ?? "test_user",
                CaseReference = caseReference,
                RecipientDescription = recipientDescription,
                ArchiveFileName = archiveFileName ?? "export.zip",
                ArchiveSha256Hash = archiveSha256Hash ?? "test_archive_hash",
                WasEncrypted = false,
                ItemCount = itemCount,
                AppVersion = appVersion ?? "1.0.0"
            };
        }
    }
}
