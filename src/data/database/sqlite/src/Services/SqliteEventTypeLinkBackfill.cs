using Microsoft.EntityFrameworkCore;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Database.DbContext;
using VideoForensics.Data.Database.Repositories;

namespace VideoForensics.Data.Database.Sqlite.Services
{
    /// <summary>
    /// SQLite implementation of <see cref="IEventTypeLinkBackfill"/>. Written as set-based SQL for SQLite, so it lives in the
    /// SQLite project; the database-agnostic code calls the interface only.
    /// </summary>
    /// <remarks>
    /// Why this is written the way it is:
    /// <list type="bullet">
    /// <item>Links are set-based (INSERT ... SELECT) so large databases never load into EF.</item>
    /// <item>NOT EXISTS makes reruns idempotent: a second run inserts nothing new.</item>
    /// <item>Keyset batches of 1000 Events keep each SQLite write transaction short.</item>
    /// <item>Motion is a fallback, so it is linked only when no non-motion type applies.</item>
    /// <item>Each batch write runs through the unit of work, so it waits in the write gate and commits atomically.</item>
    /// </list>
    /// </remarks>
    public sealed class SqliteEventTypeLinkBackfill : IEventTypeLinkBackfill
    {
        private const int BatchSize = 1000;
        private const string DerivedSource = "Derived";
        private const string MotionCode = "motion";

        private readonly IDbContextFactory<VideoForensicsDbContext> _factory;
        private readonly IUnitOfWork _unitOfWork;

        /// <summary>Creates the backfill over the given DbContext factory and unit of work.</summary>
        public SqliteEventTypeLinkBackfill(IDbContextFactory<VideoForensicsDbContext> factory, IUnitOfWork unitOfWork)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        }

        /// <inheritdoc />
        public async Task<int> RunAsync(string providerKey, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);

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

                // Why: the write gate owns the transaction, so the batch waits behind other writers and commits as one unit.
                _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
                {
                    VideoForensicsDbContext writeDb = context.Db();

                    // a. Detection links from CV data (EventDetections.DetectionType and occurrence rows).
                    inserted += await writeDb.Database.ExecuteSqlInterpolatedAsync($"""
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
                    inserted += await writeDb.Database.ExecuteSqlInterpolatedAsync($"""
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

                    return null;
                }, ct);

                lastId = last;
            }

            return inserted;
        }
    }
}
