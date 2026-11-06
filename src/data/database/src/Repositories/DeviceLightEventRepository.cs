using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Repository implementation for the append-only DeviceLightEvent history.</summary>
    public class DeviceLightEventRepository : IDeviceLightEventRepository
    {
        private readonly IDbContextFactory<VideoForensicsDbContext> _factory;
        private readonly ILogger<DeviceLightEventRepository> _logger;

        public DeviceLightEventRepository(IDbContextFactory<VideoForensicsDbContext> factory, ILogger<DeviceLightEventRepository> logger)
        {
            _factory = factory;
            _logger = logger;
        }

        public async Task<bool> RecordObservedAsync(Guid deviceId, int? secondsRemaining, DateTime observedAtUtc, CancellationToken ct)
        {
            if (secondsRemaining is not > 0)
            {
                return false;
            }

            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            try
            {
                if (!await AddObservedIfTransitionAsync(db, deviceId, secondsRemaining, observedAtUtc, ct))
                {
                    return false;
                }

                _ = await db.SaveChangesAsync(ct);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError("Error recording observed light event: {Message} ({ExceptionType})", ex.Message, ex.GetType().Name);
                throw;
            }
        }

        public async Task<bool> RecordObservedStateAsync(Guid deviceId, bool on, DateTime observedAtUtc, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            try
            {
                if (!await AddObservedStateIfChangedAsync(db, deviceId, on, observedAtUtc, ct))
                {
                    return false;
                }

                _ = await db.SaveChangesAsync(ct);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError("Error recording observed light state: {Message} ({ExceptionType})", ex.Message, ex.GetType().Name);
                throw;
            }
        }

        /// <summary>Adds an on event, or the off marker (IsOn false), to the context (no save) when the open-ended on state changes.</summary>
        internal static async Task<bool> AddObservedStateIfChangedAsync(VideoForensicsDbContext db, Guid deviceId, bool on, DateTime observedAtUtc, CancellationToken ct)
        {
            DeviceLightEvent? latest = await db.DeviceLightEvents
                .AsNoTracking()
                .Where(e => e.DeviceId == deviceId)
                .OrderByDescending(e => e.OccurredAtUtc)
                .FirstOrDefaultAsync(ct);
            bool openOn = latest is { Source: DeviceLightEventSource.Observed, IsOn: true, SecondsRemaining: null };
            if (openOn == on)
            {
                return false;
            }

            _ = db.DeviceLightEvents.Add(new DeviceLightEvent
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                OccurredAtUtc = observedAtUtc,
                Source = DeviceLightEventSource.Observed,
                IsOn = on,
                SecondsRemaining = null
            });
            return true;
        }

        /// <summary>
        /// Adds an Observed event to the context (no save) when the light is newly on. Shared with the device-details upsert so the
        /// event commits in the same transaction as the sync.
        /// </summary>
        internal static async Task<bool> AddObservedIfTransitionAsync(VideoForensicsDbContext db, Guid deviceId, int? secondsRemaining, DateTime observedAtUtc, CancellationToken ct)
        {
            if (secondsRemaining is not > 0)
            {
                return false;
            }

            // Only the latest event decides: it is on while its own reported window has not ended. A Command event has no
            // duration, so it never masks a later observed on.
            DeviceLightEvent? latest = await db.DeviceLightEvents
                .AsNoTracking()
                .Where(e => e.DeviceId == deviceId)
                .OrderByDescending(e => e.OccurredAtUtc)
                .FirstOrDefaultAsync(ct);
            if (latest is { IsOn: true, SecondsRemaining: > 0 } && latest.OccurredAtUtc.AddSeconds(latest.SecondsRemaining.Value) > observedAtUtc)
            {
                return false;
            }

            if (latest is { Source: DeviceLightEventSource.Observed, IsOn: true, SecondsRemaining: null })
            {
                return false;
            }

            _ = db.DeviceLightEvents.Add(new DeviceLightEvent
            {
                Id = Guid.NewGuid(),
                DeviceId = deviceId,
                OccurredAtUtc = observedAtUtc,
                Source = DeviceLightEventSource.Observed,
                IsOn = true,
                SecondsRemaining = secondsRemaining
            });
            return true;
        }

        public async Task AddCommandAsync(Guid deviceId, DateTime occurredAtUtc, int? secondsRemaining, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            try
            {
                _ = db.DeviceLightEvents.Add(new DeviceLightEvent
                {
                    Id = Guid.NewGuid(),
                    DeviceId = deviceId,
                    OccurredAtUtc = occurredAtUtc,
                    Source = DeviceLightEventSource.Command,
                    IsOn = true,
                    SecondsRemaining = secondsRemaining
                });
                _ = await db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error recording command light event: {Message} ({ExceptionType})", ex.Message, ex.GetType().Name);
                throw;
            }
        }

        public async Task<IReadOnlyList<DeviceLightEvent>> ListByDeviceAsync(Guid deviceId, CancellationToken ct, bool onEventsOnly = false)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await db.DeviceLightEvents
                .AsNoTracking()
                .Where(e => e.DeviceId == deviceId && (!onEventsOnly || e.IsOn))
                .OrderByDescending(e => e.OccurredAtUtc)
                .ToListAsync(ct);
        }
    }
}
