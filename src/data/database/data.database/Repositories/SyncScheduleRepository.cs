using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Repository implementation for SyncSchedule entities.</summary>
    public class SyncScheduleRepository : ISyncScheduleRepository
    {
        private readonly IDbContextFactory<VideoForensicsDbContext> _factory;
        private readonly ILogger<SyncScheduleRepository> _logger;

        /// <summary>Initializes a new instance of the SyncScheduleRepository.</summary>
        public SyncScheduleRepository(IDbContextFactory<VideoForensicsDbContext> factory, ILogger<SyncScheduleRepository> logger)
        {
            _factory = factory;
            _logger = logger;
        }

        /// <summary>Gets a sync schedule by provider account ID (includes JammingWindows).</summary>
        public async Task<SyncSchedule?> GetByProviderAccountIdAsync(Guid providerAccountId, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await db.SyncSchedules
                .Include(ss => ss.JammingWindows)
                .FirstOrDefaultAsync(ss => ss.ProviderAccountId == providerAccountId, ct);
        }

        /// <summary>Adds or updates a sync schedule. Validates that intervals meet minimum thresholds and jamming windows are well-formed.</summary>
        public async Task UpsertAsync(SyncSchedule schedule, CancellationToken ct)
        {
            // Validate intervals
            if (schedule.EventPollIntervalMinutes < SyncSchedule.MinimumPollIntervalMinutes)
            {
                throw new ArgumentException(
                    $"EventPollIntervalMinutes ({schedule.EventPollIntervalMinutes}) must be >= {SyncSchedule.MinimumPollIntervalMinutes}",
                    nameof(schedule));
            }

            if (schedule.SnapshotRssiIntervalMinutes < SyncSchedule.MinimumPollIntervalMinutes)
            {
                throw new ArgumentException(
                    $"SnapshotRssiIntervalMinutes ({schedule.SnapshotRssiIntervalMinutes}) must be >= {SyncSchedule.MinimumPollIntervalMinutes}",
                    nameof(schedule));
            }

            // Validate jamming windows
            if (schedule.JammingWindows != null)
            {
                foreach (JammingScheduleWindow window in schedule.JammingWindows)
                {
                    if (window.DayOfWeek is < 0 or > 6)
                    {
                        throw new ArgumentException(
                            $"DayOfWeek ({window.DayOfWeek}) must be in range 0..6 (Sunday=0, Saturday=6)",
                            nameof(schedule));
                    }

                    if (window.StartMinuteOfDay % 15 != 0)
                    {
                        throw new ArgumentException(
                            $"StartMinuteOfDay ({window.StartMinuteOfDay}) must be a multiple of 15",
                            nameof(schedule));
                    }

                    if (window.EndMinuteOfDay % 15 != 0)
                    {
                        throw new ArgumentException(
                            $"EndMinuteOfDay ({window.EndMinuteOfDay}) must be a multiple of 15",
                            nameof(schedule));
                    }

                    if (window.EndMinuteOfDay <= window.StartMinuteOfDay)
                    {
                        throw new ArgumentException(
                            $"EndMinuteOfDay ({window.EndMinuteOfDay}) must be greater than StartMinuteOfDay ({window.StartMinuteOfDay})",
                            nameof(schedule));
                    }

                    if (window.SnapshotRssiIntervalMinutes < SyncSchedule.MinimumPollIntervalMinutes)
                    {
                        throw new ArgumentException(
                            $"Window SnapshotRssiIntervalMinutes ({window.SnapshotRssiIntervalMinutes}) must be >= {SyncSchedule.MinimumPollIntervalMinutes}",
                            nameof(schedule));
                    }
                }
            }

            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            try
            {
                SyncSchedule? existing = await db.SyncSchedules
                    .Include(ss => ss.JammingWindows)
                    .FirstOrDefaultAsync(ss => ss.ProviderAccountId == schedule.ProviderAccountId, ct);

                if (existing != null)
                {
                    existing.EventPollIntervalMinutes = schedule.EventPollIntervalMinutes;
                    existing.SnapshotRssiIntervalMinutes = schedule.SnapshotRssiIntervalMinutes;
                    existing.IsEnabled = schedule.IsEnabled;
                    existing.UseAdvancedJammingSchedule = schedule.UseAdvancedJammingSchedule;
                    existing.EventNextRunUtc = schedule.EventNextRunUtc;
                    existing.EventLastRunUtc = schedule.EventLastRunUtc;
                    existing.SnapshotNextRunUtc = schedule.SnapshotNextRunUtc;
                    existing.SnapshotLastRunUtc = schedule.SnapshotLastRunUtc;

                    // Update jamming windows: remove all old ones and add new ones
                    existing.JammingWindows.Clear();
                    if (schedule.JammingWindows != null)
                    {
                        foreach (JammingScheduleWindow window in schedule.JammingWindows)
                        {
                            existing.JammingWindows.Add(window);
                        }
                    }

                    _ = db.SyncSchedules.Update(existing);
                }
                else
                {
                    _ = db.SyncSchedules.Add(schedule);
                }

                _ = await db.SaveChangesAsync(ct);
                _logger.LogInformation("Sync schedule upserted for provider account: {ProviderAccountId}", schedule.ProviderAccountId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error upserting sync schedule for provider account: {ProviderAccountId}", schedule.ProviderAccountId);
                throw;
            }
        }

        /// <summary>Deletes a sync schedule by provider account ID.</summary>
        public async Task DeleteAsync(Guid providerAccountId, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            try
            {
                SyncSchedule? schedule = await db.SyncSchedules.FirstOrDefaultAsync(ss => ss.ProviderAccountId == providerAccountId, ct);
                if (schedule != null)
                {
                    _ = db.SyncSchedules.Remove(schedule);
                    _ = await db.SaveChangesAsync(ct);
                    _logger.LogInformation("Sync schedule deleted for provider account: {ProviderAccountId}", providerAccountId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting sync schedule for provider account: {ProviderAccountId}", providerAccountId);
                throw;
            }
        }
    }
}
