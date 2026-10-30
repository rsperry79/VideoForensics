using Microsoft.EntityFrameworkCore;

using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.ReferenceData
{
    /// <summary>
    /// Derives EventTypeLinks rows from existing event, detection and occurrence data.
    /// </summary>
    /// <remarks>
    /// Why this is written the way it is:
    /// <list type="bullet">
    /// <item>Links are set-based (INSERT ... SELECT) so 1M-row databases never load into EF.</item>
    /// <item>NOT EXISTS makes reruns idempotent: a second run inserts nothing new.</item>
    /// <item>Keyset batches of 1000 Events keep each SQLite write transaction short.</item>
    /// <item>Motion is a fallback, so it is linked only when no non-motion type applies.</item>
    /// </list>
    /// Existing links are never deleted or updated here. Re-derivation with deletes is a separate step.
    /// </remarks>
    public static class EventTypeBackfill
    {
        private const int BatchSize = 1000;
        private const string DerivedSource = "Derived";
        private const string MotionCode = "motion";

        /// <summary>Inserts derived EventTypeLinks for all events and returns the number of rows inserted.</summary>
        public static async Task<int> RunAsync(
            IDbContextFactory<VideoForensicsDbContext> factory,
            string providerKey,
            CancellationToken ct)
        {
            await using var db = await factory.CreateDbContextAsync(ct);

            DateTime nowUtc = DateTime.UtcNow;
            int inserted = 0;
            Guid? lastId = null;

            while (true)
            {
                ct.ThrowIfCancellationRequested();

                // Keyset page: the next BatchSize Event Ids after the last one processed.
                // Guid ordering is text ordering in SQLite, which matches the comparison in the batch statements.
                List<Guid> ids = lastId is null
                    ? await db.Database.SqlQuery<Guid>(
                        $"SELECT Id AS Value FROM Events ORDER BY Id LIMIT {BatchSize}").ToListAsync(ct)
                    : await db.Database.SqlQuery<Guid>(
                        $"SELECT Id AS Value FROM Events WHERE Id > {lastId.Value} ORDER BY Id LIMIT {BatchSize}").ToListAsync(ct);

                if (ids.Count == 0)
                {
                    break;
                }

                // The ordered page is contiguous in Id, so a range covers exactly this batch.
                Guid first = ids[0];
                Guid last = ids[^1];

                await using var tx = await db.Database.BeginTransactionAsync(ct);

                // a. Detection links from CV data (EventDetections.DetectionType and occurrence rows).
                inserted += await db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO EventTypeLinks (EventId, EventTypeCode, Source, CreatedAtUtc)
                    SELECT DISTINCT src.EventId, m.EventTypeCode, {DerivedSource}, {nowUtc}
                    FROM (
                        SELECT d.EventId AS EventId, d.DetectionType AS RawType
                        FROM EventDetections d
                        WHERE d.EventId >= {first} AND d.EventId <= {last}
                        UNION ALL
                        SELECT d.EventId AS EventId, o.DetectionType AS RawType
                        FROM EventDetectionTypeOccurrences o
                        JOIN EventDetections d ON d.Id = o.EventDetectionId
                        WHERE d.EventId >= {first} AND d.EventId <= {last}
                    ) src
                    JOIN ProviderEventTypeMappings m
                        ON m.ProviderKey = {providerKey} AND m.ProviderValue = src.RawType
                    WHERE m.EventTypeCode <> {MotionCode}
                      AND NOT EXISTS (
                          SELECT 1 FROM EventTypeLinks l
                          WHERE l.EventId = src.EventId AND l.EventTypeCode = m.EventTypeCode)
                    """, ct);

                // b. Motion fallback: only for events with no non-motion link after step a.
                inserted += await db.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO EventTypeLinks (EventId, EventTypeCode, Source, CreatedAtUtc)
                    SELECT e.Id, {MotionCode}, {DerivedSource}, {nowUtc}
                    FROM Events e
                    WHERE e.Id >= {first} AND e.Id <= {last}
                      AND NOT EXISTS (
                          SELECT 1 FROM EventTypeLinks l
                          WHERE l.EventId = e.Id AND l.EventTypeCode <> {MotionCode})
                      AND NOT EXISTS (
                          SELECT 1 FROM EventTypeLinks l
                          WHERE l.EventId = e.Id AND l.EventTypeCode = {MotionCode})
                      AND (
                          LOWER(e.EventType) = {MotionCode}
                          OR EXISTS (
                              SELECT 1
                              FROM EventDetections d
                              JOIN ProviderEventTypeMappings m
                                  ON m.ProviderKey = {providerKey} AND m.ProviderValue = d.DetectionType
                              WHERE d.EventId = e.Id AND m.EventTypeCode = {MotionCode})
                          OR EXISTS (
                              SELECT 1
                              FROM EventDetections d
                              JOIN EventDetectionTypeOccurrences o ON o.EventDetectionId = d.Id
                              JOIN ProviderEventTypeMappings m
                                  ON m.ProviderKey = {providerKey} AND m.ProviderValue = o.DetectionType
                              WHERE d.EventId = e.Id AND m.EventTypeCode = {MotionCode})
                      )
                    """, ct);

                await tx.CommitAsync(ct);

                lastId = last;
            }

            return inserted;
        }
    }
}
