using Microsoft.EntityFrameworkCore;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Repository implementation for OperatorNotificationPreference entities (per-operator push notification settings).</summary>
    public class OperatorNotificationPreferenceRepository : IOperatorNotificationPreferenceRepository
    {
        private readonly IDbContextFactory<VideoForensicsDbContext> _contextFactory;

        /// <summary>Initializes a new instance of the OperatorNotificationPreferenceRepository.</summary>
        public OperatorNotificationPreferenceRepository(IDbContextFactory<VideoForensicsDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        /// <summary>Gets the notification preferences for an operator, or null if not yet set.</summary>
        public async Task<OperatorNotificationPreference?> GetAsync(Guid operatorId, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _contextFactory.CreateDbContextAsync(ct);
            return await db.OperatorNotificationPreferences.FirstOrDefaultAsync(p => p.OperatorId == operatorId, ct);
        }

        /// <summary>Inserts or updates the notification preferences for the given operator (keyed by OperatorId).</summary>
        public async Task UpsertAsync(OperatorNotificationPreference preferences, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _contextFactory.CreateDbContextAsync(ct);
            OperatorNotificationPreference? existing = await db.OperatorNotificationPreferences
                .FirstOrDefaultAsync(p => p.OperatorId == preferences.OperatorId, ct);

            if (existing != null)
            {
                existing.PushEnabled = preferences.PushEnabled;
                existing.MinimumSeverity = preferences.MinimumSeverity;
                _ = db.OperatorNotificationPreferences.Update(existing);
            }
            else
            {
                _ = await db.OperatorNotificationPreferences.AddAsync(preferences, ct);
            }

            _ = await db.SaveChangesAsync(ct);
        }
    }
}
