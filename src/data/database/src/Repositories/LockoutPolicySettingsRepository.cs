using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Repository implementation for LockoutPolicySettings singleton entity.</summary>
    public class LockoutPolicySettingsRepository : ILockoutPolicySettingsRepository
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<LockoutPolicySettingsRepository> _logger;

        public LockoutPolicySettingsRepository(IUnitOfWork unitOfWork, ILogger<LockoutPolicySettingsRepository> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<LockoutPolicySettings> GetAsync(CancellationToken ct)
        {
            return await _unitOfWork.ReadAsync<LockoutPolicySettings>(async context =>
            {
                VideoForensicsDbContext db = context.Db();

                // Singleton table: OrderBy silences EF 10103 (First without OrderBy) and gives a deterministic row.
                LockoutPolicySettings? settings = await db.LockoutPolicySettings.OrderBy(s => s.Id).FirstOrDefaultAsync(ct);

                if (settings == null)
                {
                    // Return default instance without persisting
                    return new LockoutPolicySettings
                    {
                        Id = Guid.Empty, // Sentinel value indicating not-yet-persisted
                        MaxFailedAttempts = 5,
                        LockoutDurationMinutes = 15,
                        BlockedCountryCodes = null,
                        FailClosedOnLookupError = false,
                        AutoBanEnabled = false,
                        AutoBanDistinctAccountThreshold = 3,
                        AutoBanWindowMinutes = 60,
                        AutoBanDurationMinutes = 1440,
                        UpdatedAtUtc = DateTime.UtcNow,
                        UpdatedByOperatorId = null
                    };
                }

                return settings;
            }, ct);
        }

        public async Task UpsertAsync(LockoutPolicySettings settings, CancellationToken ct)
        {
            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                VideoForensicsDbContext db = context.Db();

                LockoutPolicySettings? existing = await db.LockoutPolicySettings.OrderBy(s => s.Id).FirstOrDefaultAsync(ct);

                if (existing == null)
                {
                    // Ensure a valid Id for insertion
                    if (settings.Id == Guid.Empty)
                    {
                        settings.Id = Guid.NewGuid();
                    }

                    settings.UpdatedAtUtc = DateTime.UtcNow;

                    _ = db.LockoutPolicySettings.Add(settings);
                    _logger.LogDebug("Lockout policy settings created");
                }
                else
                {
                    // Update existing row
                    existing.MaxFailedAttempts = settings.MaxFailedAttempts;
                    existing.LockoutDurationMinutes = settings.LockoutDurationMinutes;
                    existing.BlockedCountryCodes = settings.BlockedCountryCodes;
                    existing.FailClosedOnLookupError = settings.FailClosedOnLookupError;
                    existing.AutoBanEnabled = settings.AutoBanEnabled;
                    existing.AutoBanDistinctAccountThreshold = settings.AutoBanDistinctAccountThreshold;
                    existing.AutoBanWindowMinutes = settings.AutoBanWindowMinutes;
                    existing.AutoBanDurationMinutes = settings.AutoBanDurationMinutes;
                    existing.UpdatedAtUtc = DateTime.UtcNow;
                    existing.UpdatedByOperatorId = settings.UpdatedByOperatorId;

                    _logger.LogDebug("Lockout policy settings updated");
                }

                _ = await db.SaveChangesAsync(ct);
                return null;
            }, ct);
        }
    }
}
