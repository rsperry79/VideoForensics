using Microsoft.EntityFrameworkCore;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Repository implementation for OperatorNotificationPreference entities (per-operator push notification settings).</summary>
    public class OperatorNotificationPreferenceRepository : IOperatorNotificationPreferenceRepository
    {
        private readonly IUnitOfWork _unitOfWork;

        /// <summary>Initializes a new instance of the OperatorNotificationPreferenceRepository.</summary>
        public OperatorNotificationPreferenceRepository(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        /// <summary>Gets the notification preferences for an operator, or null if not yet set.</summary>
        public Task<OperatorNotificationPreference?> GetAsync(Guid operatorId, CancellationToken ct)
        {
            return _unitOfWork.ReadAsync(context => context.Db().OperatorNotificationPreferences.FirstOrDefaultAsync(p => p.OperatorId == operatorId, ct), ct);
        }

        /// <summary>Inserts or updates the notification preferences for the given operator (keyed by OperatorId).</summary>
        public async Task UpsertAsync(OperatorNotificationPreference preferences, CancellationToken ct)
        {
            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                VideoForensicsDbContext db = context.Db();
                OperatorNotificationPreference? existing = await db.OperatorNotificationPreferences
                    .FirstOrDefaultAsync(p => p.OperatorId == preferences.OperatorId, ct);

                if (existing != null)
                {
                    existing.PushEnabled = preferences.PushEnabled;
                    existing.MinimumSeverity = preferences.MinimumSeverity;
                    existing.EmailEnabled = preferences.EmailEnabled;
                    existing.EmailEventTypes = preferences.EmailEventTypes;
                    _ = db.OperatorNotificationPreferences.Update(existing);
                }
                else
                {
                    _ = await db.OperatorNotificationPreferences.AddAsync(preferences, ct);
                }

                _ = await db.SaveChangesAsync(ct);
                return null;
            }, ct);
        }
    }
}
