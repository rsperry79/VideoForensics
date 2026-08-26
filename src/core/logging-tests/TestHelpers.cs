using System;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Core.Logging.Tests
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
    }
}
