using System.Data.Common;
using System.Diagnostics;
using System.Text;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;
using VideoForensics.Data.Database.Repositories;

using Xunit;

namespace VideoForensics.Data.Database.Tests.Repositories
{
    /// <summary>
    /// Measurement probe, not a behaviour test. Runs against a COPY of the live database named by the VF_PERF_DB
    /// environment variable and writes timings to the file named by VF_PERF_OUT. Skipped when VF_PERF_DB is unset.
    /// </summary>
    public class WritePathTimingProbe
    {
        [Fact]
        public async Task MeasureActionLogAppendCost()
        {
            string? dbPath = Environment.GetEnvironmentVariable("VF_PERF_DB");
            string? outPath = Environment.GetEnvironmentVariable("VF_PERF_OUT");
            if (string.IsNullOrWhiteSpace(dbPath) || string.IsNullOrWhiteSpace(outPath))
            {
                return;
            }

            var sb = new StringBuilder();
            DbContextOptions<VideoForensicsDbContext> options = new DbContextOptionsBuilder<VideoForensicsDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .Options;

            sb.AppendLine($"rows in ActionLogEntries: {await CountAsync(options, "ActionLogEntries")}");
            sb.AppendLine($"rows in MediaItems: {await CountAsync(options, "MediaItems")}");

            // 1. Tail lookup cost, as AppendAsync runs it.
            await using (var db = new VideoForensicsDbContext(options))
            {
                _ = await ActionLogTail.Latest(db).FirstOrDefaultAsync();
                var clock = Stopwatch.StartNew();
                for (int i = 0; i < 20; i++)
                {
                    _ = await ActionLogTail.Latest(db).FirstOrDefaultAsync();
                }

                sb.AppendLine($"tail lookup avg over 20 after warm-up: {clock.Elapsed.TotalMilliseconds / 20:F2} ms");

                DbConnection conn = db.Database.GetDbConnection();
                await conn.OpenAsync();
                await using DbCommand plan = conn.CreateCommand();
                plan.CommandText = "EXPLAIN QUERY PLAN SELECT \"Id\" FROM \"ActionLogEntries\" ORDER BY \"TimestampUtc\" DESC, \"Id\" DESC LIMIT 1";
                await using DbDataReader reader = await plan.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    sb.AppendLine($"plan: {reader.GetString(3)}");
                }
            }

            // 2. One transaction, one append, one commit: the shape of a single-write unit of work.
            sb.AppendLine($"single append in its own transaction: {await TimeAppendsAsync(options, 1, savePerAppend: true):F1} ms");

            // 3. Ten appends in one transaction, saving after each (AppendAsync's current behaviour).
            sb.AppendLine($"10 appends, save after each: {await TimeAppendsAsync(options, 10, savePerAppend: true):F1} ms");

            // 4. Ten appends in one transaction, one save at the end (the hypothesised cheaper shape).
            sb.AppendLine($"10 appends, one save: {await TimeAppendsAsync(options, 10, savePerAppend: false):F1} ms");

            File.WriteAllText(outPath, sb.ToString());
        }

        [Fact]
        public async Task MeasureDownloadAndRingPaths()
        {
            string? dbPath = Environment.GetEnvironmentVariable("VF_PERF_DB");
            string? outPath = Environment.GetEnvironmentVariable("VF_PERF_OUT");
            if (string.IsNullOrWhiteSpace(dbPath) || string.IsNullOrWhiteSpace(outPath))
            {
                return;
            }

            DbContextOptions<VideoForensicsDbContext> options = new DbContextOptionsBuilder<VideoForensicsDbContext>()
                .UseSqlite($"Data Source={dbPath}")
                .Options;

            // Real sample values from the copy, so the queries hit the same index keys production does.
            DownloadEvent download;
            MediaItem media;
            RingLocationHistoryEvent ring;
            await using (var db = new VideoForensicsDbContext(options))
            {
                download = await db.DownloadEvents.AsNoTracking().FirstAsync();
                media = await db.MediaItems.AsNoTracking().Where(m => m.DownloadEventId != null).FirstAsync();
                ring = await db.RingLocationHistoryEvents.AsNoTracking().FirstAsync();
            }

            const int iterations = 5;
            var samples = new Dictionary<string, List<double>>();

            // Each iteration runs the same steps as RecordDownloadEventAsync inside one transaction, then rolls back.
            for (int i = 0; i < iterations; i++)
            {
                await using var db = new VideoForensicsDbContext(options);
                using var tx = await db.Database.BeginTransactionAsync();

                await Timed(samples, "download: lookup by provider event id", async () =>
                    _ = await db.DownloadEvents.AsNoTracking().FirstOrDefaultAsync(d => d.DeviceId == download.DeviceId && d.ProviderEventId == download.ProviderEventId));

                await Timed(samples, "media: same-recording range lookup", async () =>
                    _ = await db.MediaItems.AsNoTracking()
                        .Where(m => m.DeviceId == media.DeviceId && m.RecordedAtUtc >= media.RecordedAtUtc && m.RecordedAtUtc <= media.RecordedAtUtc)
                        .ToListAsync());

                await Timed(samples, "device: get by id", async () =>
                    _ = await db.Devices.AsNoTracking().FirstOrDefaultAsync(d => d.Id == download.DeviceId));

                await Timed(samples, "action log: tail lookup", async () =>
                    _ = await ActionLogTail.Latest(db).FirstOrDefaultAsync());

                await Timed(samples, "action log: append + save", async () =>
                {
                    _ = db.ActionLogEntries.Add(new ActionLogEntry
                    {
                        Id = Guid.NewGuid(), Actor = "perf-probe", ActorType = ActorType.Human, Action = "PerfProbe",
                        EntityType = "PerfProbe", EntityId = null, DetailsJson = null, TimestampUtc = DateTime.UtcNow,
                        PreviousEntryHash = null, EntryHash = "perf-probe"
                    });
                    _ = await db.SaveChangesAsync();
                });

                await Timed(samples, "ring: replace one location-history event", async () =>
                {
                    List<Guid> existing = await db.RingLocationHistoryEvents
                        .Where(e => e.ProviderLocationId == ring.ProviderLocationId && e.ProviderEventId == ring.ProviderEventId)
                        .Select(e => e.Id)
                        .ToListAsync();
                    _ = await db.RingLocationHistoryEvents.Where(e => existing.Contains(e.Id)).ExecuteDeleteAsync();
                    _ = await db.SaveChangesAsync();
                });

                await tx.RollbackAsync();
            }

            var sb = new StringBuilder();
            foreach (KeyValuePair<string, List<double>> kv in samples)
            {
                sb.AppendLine($"{kv.Key}: avg {kv.Value.Average():F2} ms, min {kv.Value.Min():F2}, max {kv.Value.Max():F2} (n={kv.Value.Count})");
            }

            File.WriteAllText(outPath + ".paths", sb.ToString());
        }

        private static async Task Timed(Dictionary<string, List<double>> samples, string label, Func<Task> step)
        {
            var clock = Stopwatch.StartNew();
            await step();
            clock.Stop();
            if (!samples.TryGetValue(label, out List<double>? list))
            {
                list = [];
                samples[label] = list;
            }

            list.Add(clock.Elapsed.TotalMilliseconds);
        }

        private static async Task<int> CountAsync(DbContextOptions<VideoForensicsDbContext> options, string table)
        {
            await using var db = new VideoForensicsDbContext(options);
            var conn = db.Database.GetDbConnection();
            await conn.OpenAsync();
            await using DbCommand cmd = conn.CreateCommand();
            cmd.CommandText = $"SELECT COUNT(*) FROM \"{table}\"";
            return Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        private static async Task<double> TimeAppendsAsync(DbContextOptions<VideoForensicsDbContext> options, int count, bool savePerAppend)
        {
            var clock = Stopwatch.StartNew();
            await using var db = new VideoForensicsDbContext(options);
            using var tx = await db.Database.BeginTransactionAsync();
            for (int i = 0; i < count; i++)
            {
                _ = db.ActionLogEntries.Add(new ActionLogEntry
                {
                    Id = Guid.NewGuid(),
                    Actor = "perf-probe",
                    ActorType = ActorType.Human,
                    Action = "PerfProbe",
                    EntityType = "PerfProbe",
                    EntityId = null,
                    DetailsJson = null,
                    TimestampUtc = DateTime.UtcNow,
                    PreviousEntryHash = null,
                    EntryHash = "perf-probe"
                });
                if (savePerAppend)
                {
                    _ = await db.SaveChangesAsync();
                }
            }

            _ = await db.SaveChangesAsync();
            await tx.RollbackAsync();
            return clock.Elapsed.TotalMilliseconds;
        }
    }
}
