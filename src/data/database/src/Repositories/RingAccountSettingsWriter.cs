using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>
    /// Replaces Ring account settings graphs. Each write runs in one transaction: the existing parent rows are
    /// removed with their child rows (loaded with Include so cascade deletes them), then the new graph is inserted.
    /// If the insert fails the transaction rolls back and the previous graph is left in place.
    /// </summary>
    public class RingAccountSettingsWriter : IRingAccountSettingsWriter
    {
        private readonly IDbContextFactory<VideoForensicsDbContext> _factory;

        public RingAccountSettingsWriter(IDbContextFactory<VideoForensicsDbContext> factory)
        {
            _factory = factory;
        }

        public Task ReplaceProfileAsync(RingAccountProfile profile, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(profile);

            return ReplaceAsync(async (db, token) =>
            {
                List<RingAccountProfile> existing = await db.RingAccountProfiles
                    .Include(p => p.SessionFeatures)
                    .Include(p => p.Flags).ThenInclude(f => f.Items)
                    .Where(p => p.RingAccountId == profile.RingAccountId)
                    .ToListAsync(token);
                await SwapAsync(db, db.RingAccountProfiles, existing, profile, token);
            }, ct);
        }

        public Task ReplaceRingtonesAsync(Guid ringAccountId, IReadOnlyList<RingAccountRingtone> ringtones, RingAccountRingtoneDefaults defaults, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(ringtones);
            ArgumentNullException.ThrowIfNull(defaults);

            return ReplaceAsync(async (db, token) =>
            {
                List<RingAccountRingtone> existingRingtones = await db.RingAccountRingtones
                    .Include(r => r.SupportedDeviceKinds)
                    .Where(r => r.RingAccountId == ringAccountId)
                    .ToListAsync(token);
                List<RingAccountRingtoneDefaults> existingDefaults = await db.RingAccountRingtoneDefaults
                    .Where(d => d.RingAccountId == ringAccountId)
                    .ToListAsync(token);

                db.RingAccountRingtones.RemoveRange(existingRingtones);
                db.RingAccountRingtoneDefaults.RemoveRange(existingDefaults);
                _ = await db.SaveChangesAsync(token);

                db.RingAccountRingtones.AddRange(ringtones);
                _ = db.RingAccountRingtoneDefaults.Add(defaults);
                _ = await db.SaveChangesAsync(token);
            }, ct);
        }

        public Task ReplaceLightGroupSetAsync(LightGroupSet set, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(set);

            return ReplaceAsync(async (db, token) =>
            {
                List<LightGroupSet> existing = await db.LightGroupSets
                    .Include(s => s.Groups).ThenInclude(g => g.Devices)
                    .Where(s => s.ProviderLocationId == set.ProviderLocationId)
                    .ToListAsync(token);
                await SwapAsync(db, db.LightGroupSets, existing, set, token);
            }, ct);
        }

        public Task ReplaceLocationModeAsync(RingLocationMode mode, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(mode);

            return ReplaceAsync(async (db, token) =>
            {
                List<RingLocationMode> existing = await db.RingLocationModes
                    .Include(m => m.NotYetParticipating)
                    .Where(m => m.ProviderLocationId == mode.ProviderLocationId)
                    .ToListAsync(token);
                await SwapAsync(db, db.RingLocationModes, existing, mode, token);
            }, ct);
        }

        public Task ReplaceLocationModeSettingsAsync(RingLocationModeSettings settings, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(settings);

            return ReplaceAsync(async (db, token) =>
            {
                List<RingLocationModeSettings> existing = await db.RingLocationModeSettings
                    .Include(s => s.SupportedActionGroups)
                    .Include(s => s.Entries).ThenInclude(e => e.Actions)
                    .Where(s => s.ProviderLocationId == settings.ProviderLocationId)
                    .ToListAsync(token);
                await SwapAsync(db, db.RingLocationModeSettings, existing, settings, token);
            }, ct);
        }

        public Task ReplaceLocationModeSharingAsync(RingLocationModeSharing sharing, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(sharing);

            return ReplaceAsync(async (db, token) =>
            {
                List<RingLocationModeSharing> existing = await db.RingLocationModeSharings
                    .Where(s => s.ProviderLocationId == sharing.ProviderLocationId)
                    .ToListAsync(token);
                await SwapAsync(db, db.RingLocationModeSharings, existing, sharing, token);
            }, ct);
        }

        public Task ReplaceAccountMonitoringStatusAsync(RingAccountMonitoringStatus status, CancellationToken ct)
        {
            ArgumentNullException.ThrowIfNull(status);

            return ReplaceAsync(async (db, token) =>
            {
                List<RingAccountMonitoringStatus> existing = await db.RingAccountMonitoringStatuses
                    .Include(s => s.CompatibleMonitoringPlans)
                    .Where(s => s.ProviderLocationId == status.ProviderLocationId)
                    .ToListAsync(token);
                await SwapAsync(db, db.RingAccountMonitoringStatuses, existing, status, token);
            }, ct);
        }

        private async Task ReplaceAsync(Func<VideoForensicsDbContext, CancellationToken, Task> replace, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            await using IDbContextTransaction transaction = await db.Database.BeginTransactionAsync(ct);
            await replace(db, ct);
            await transaction.CommitAsync(ct);
        }

        // Deletes first and saves, then inserts: a unique index on the parent key (one profile per account,
        // one row per location) would otherwise see the new row before the old one is gone.
        private static async Task SwapAsync<T>(VideoForensicsDbContext db, DbSet<T> set, List<T> existing, T replacement, CancellationToken ct)
            where T : class
        {
            set.RemoveRange(existing);
            _ = await db.SaveChangesAsync(ct);

            _ = set.Add(replacement);
            _ = await db.SaveChangesAsync(ct);
        }
    }
}
