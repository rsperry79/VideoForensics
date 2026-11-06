using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Common.Matching;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Result of a ding id repair run. Counts are always filled; the link totals are zero on a dry run.</summary>
    /// <param name="DoorbotHistory">Per-outcome counts for the doorbot history feed, using exact equality.</param>
    /// <param name="EventsLinked">Doorbot history rows linked to the Events row that already holds their ding id.</param>
    /// <param name="NoEventRow">Exactly matched doorbot history rows whose device has no Events row with that ding id. Nothing is created for them.</param>
    /// <param name="Applied">True when the run wrote changes; false for a dry run.</param>
    /// <param name="Aborted">True when the hold check failed before the commit or before the first write. Nothing was committed and the counts are zero.</param>
    public sealed record DingIdRepairReport(
        DingIdMatchCounts DoorbotHistory,
        int EventsLinked,
        int NoEventRow,
        bool Applied,
        bool Aborted = false);

    /// <summary>
    /// Links doorbot history rows to the Events rows that already hold their string ding id. Each doorbot history row is
    /// matched by exact equality to the string ding ids of its own device. Only exact, single matches are linked; the
    /// repair writes only RingDoorbotHistoryEvents.LinkedEventId. It renames nothing and deletes nothing.
    /// The class name is historical.
    /// </summary>
    public sealed class DingIdRepairService
    {
        /// <summary>
        /// Matches every doorbot history row to its device's ding ids. When <paramref name="apply"/> is true, takes a
        /// backup first, then applies all links in one transaction.
        /// </summary>
        /// <param name="factory">Creates the context used for reads and, on apply, the single write transaction.</param>
        /// <param name="apply">False for a dry run: counts only, no backup, no writes.</param>
        /// <param name="backup">Invoked once before any write when applying. An exception stops the run before changes.</param>
        /// <param name="ct">Cancellation token for the run.</param>
        /// <param name="confirmHold">Invoked once on apply, after all writes and before the commit. Returning false rolls back and reports <see cref="DingIdRepairReport.Aborted"/>. Null means no check (dry runs take no hold).</param>
        /// <param name="beforeWrites">Invoked once on apply, after the backup and before the first write. Returning false stops the run with <see cref="DingIdRepairReport.Aborted"/> and nothing written. Null means no check.</param>
        public async Task<DingIdRepairReport> RunAsync(
            IDbContextFactory<VideoForensicsDbContext> factory,
            bool apply,
            Func<CancellationToken, Task> backup,
            CancellationToken ct,
            Func<CancellationToken, Task<bool>>? confirmHold = null,
            Func<CancellationToken, Task<bool>>? beforeWrites = null)
        {
            ArgumentNullException.ThrowIfNull(factory);
            ArgumentNullException.ThrowIfNull(backup);

            await using VideoForensicsDbContext db = await factory.CreateDbContextAsync(ct);

            // Two flat reads, then the nested device id is joined in memory. Reading the nested row as a correlated
            // subquery per history row would be one SQL round trip per row; the repair runs once, so a flat read is simpler.
            List<(Guid Id, string ProviderEventId)> historyRows = (await db.RingDoorbotHistoryEvents.AsNoTracking()
                .Where(h => h.ProviderEventId != null)
                .Select(h => new { h.Id, h.ProviderEventId })
                .ToListAsync(ct))
                .Select(h => (h.Id, h.ProviderEventId!))
                .ToList();

            var doorbotByHistoryRow = new Dictionary<Guid, long?>();
            foreach (var nested in await db.RingEventDoorbots.AsNoTracking()
                .Where(x => x.Source == RingEventSources.DoorbotHistory)
                .Select(x => new { x.SourceRowId, x.DoorbotId })
                .ToListAsync(ct))
            {
                _ = doorbotByHistoryRow.TryAdd(nested.SourceRowId, nested.DoorbotId);
            }

            // Candidates and the device's Guid are the same for every row of one doorbot, so resolve each doorbot once.
            var deviceByDoorbot = new Dictionary<long, (Guid? DeviceId, List<string> Candidates)>();
            var results = new List<DingIdMatchResult>(historyRows.Count);
            var matched = new List<(Guid HistoryId, Guid DeviceId, string DingId)>();

            foreach ((Guid historyId, string historyProviderEventId) in historyRows)
            {
                if (!doorbotByHistoryRow.TryGetValue(historyId, out long? doorbotId) || doorbotId is null)
                {
                    results.Add(new DingIdMatchResult(DingIdMatchOutcome.Unmatched, null));
                    continue;
                }

                if (!deviceByDoorbot.TryGetValue(doorbotId.Value, out (Guid? DeviceId, List<string> Candidates) resolved))
                {
                    string providerDeviceId = doorbotId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    Guid? deviceId = await db.Devices.AsNoTracking()
                        .Where(d => d.ProviderDeviceId == providerDeviceId)
                        .Select(d => (Guid?)d.Id)
                        .FirstOrDefaultAsync(ct);
                    resolved = (deviceId, deviceId is null ? new List<string>() : DingIdCandidates.ForDoorbot(db, doorbotId.Value));
                    deviceByDoorbot[doorbotId.Value] = resolved;
                }

                DingIdMatchResult result = DingIdPrefixMatcher.Match(historyProviderEventId, resolved.Candidates);
                results.Add(result);
                if (result.Outcome == DingIdMatchOutcome.Matched && resolved.DeviceId is Guid device)
                {
                    matched.Add((historyId, device, result.DingId!));
                }
            }

            DingIdMatchCounts counts = DingIdPrefixMatcher.CountOutcomes(results);
            if (!apply)
            {
                return new DingIdRepairReport(counts, 0, 0, false);
            }

            await backup(ct);

            // Why: the backup can take a long time on a large database, so the hold is checked again once it is done and before any write starts.
            if (beforeWrites is not null && !await beforeWrites(ct))
            {
                return new DingIdRepairReport(counts, 0, 0, Applied: false, Aborted: true);
            }

            // One transaction: a failure part-way leaves the database as it was before the run.
            await using IDbContextTransaction transaction = await db.Database.BeginTransactionAsync(ct);
            try
            {
                int eventsLinked = 0;
                int noEventRow = 0;

                foreach ((Guid historyId, Guid deviceId, string dingId) in matched)
                {
                    // Why: Events rows already hold the string ding id, so the link is found by equality, never by renaming a row.
                    Guid? eventId = await db.Events.AsNoTracking()
                        .Where(e => e.DeviceId == deviceId && e.ProviderEventId == dingId)
                        .Select(e => (Guid?)e.Id)
                        .FirstOrDefaultAsync(ct);
                    if (eventId is null)
                    {
                        noEventRow++;
                        continue;
                    }

                    RingDoorbotHistoryEvent history = await db.RingDoorbotHistoryEvents.FirstAsync(h => h.Id == historyId, ct);
                    history.LinkedEventId = eventId.Value;
                    eventsLinked++;
                }

                _ = await db.SaveChangesAsync(ct);

                // Why: the hold can end while the writes are staged. Checking right before the commit means a lost hold leaves nothing committed.
                if (confirmHold is not null && !await confirmHold(ct))
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                    return new DingIdRepairReport(counts, 0, 0, Applied: false, Aborted: true);
                }

                await transaction.CommitAsync(ct);

                return new DingIdRepairReport(counts, eventsLinked, noEventRow, true);
            }
            catch
            {
                // Roll back with a token that cannot be cancelled, so a cancelled run still leaves no partial writes.
                await transaction.RollbackAsync(CancellationToken.None);
                throw;
            }
        }
    }
}
