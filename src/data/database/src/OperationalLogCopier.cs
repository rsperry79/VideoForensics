using System.Linq.Expressions;

using Microsoft.EntityFrameworkCore;

using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database
{
    /// <summary>
    /// Copies operational rows (provider error rows, call records, telemetry) from the main database into logs.db. Rows are
    /// copied, not moved: the originals stay put. A row is copied only when logs.db does not already have its Id, so
    /// running it again adds nothing.
    /// </summary>
    public static class OperationalLogCopier
    {
        /// <summary>Returns how many rows were copied across all three tables.</summary>
        public static async Task<int> CopyAsync(
            IDbContextFactory<VideoForensicsDbContext> mainFactory,
            IDbContextFactory<VideoForensicsLogsDbContext> logsFactory,
            CancellationToken ct)
        {
            await using VideoForensicsDbContext source = await mainFactory.CreateDbContextAsync(ct);
            await using VideoForensicsLogsDbContext target = await logsFactory.CreateDbContextAsync(ct);

            int copied = 0;
            copied += await CopyMissingAsync(source.ProviderApiErrorLogs, target.ProviderApiErrorLogs, e => e.Id, target, ct);
            copied += await CopyMissingAsync(source.ProviderApiCallRecords, target.ProviderApiCallRecords, c => c.Id, target, ct);
            copied += await CopyMissingAsync(source.LiveViewTelemetrySamples, target.LiveViewTelemetrySamples, s => s.Id, target, ct);
            return copied;
        }

        private static async Task<int> CopyMissingAsync<T>(
            DbSet<T> source,
            DbSet<T> target,
            Expression<Func<T, Guid>> key,
            VideoForensicsLogsDbContext targetContext,
            CancellationToken ct)
            where T : class
        {
            Func<T, Guid> keyOf = key.Compile();
            HashSet<Guid> existing = (await target.Select(key).ToListAsync(ct)).ToHashSet();

            List<T> missing = (await source.AsNoTracking().ToListAsync(ct))
                .Where(row => !existing.Contains(keyOf(row)))
                .ToList();
            if (missing.Count == 0)
            {
                return 0;
            }

            target.AddRange(missing);
            _ = await targetContext.SaveChangesAsync(ct);
            return missing.Count;
        }
    }
}
