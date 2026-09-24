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
        private readonly IDbContextFactory<VideoForensicsDbContext> _factory;
        private readonly ILogger<LockoutPolicySettingsRepository> _logger;

        public LockoutPolicySettingsRepository(IDbContextFactory<VideoForensicsDbContext> factory, ILogger<LockoutPolicySettingsRepository> logger)
        {
            _factory = factory;
            _logger = logger;
        }

        public async Task<LockoutPolicySettings> GetAsync(CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            LockoutPolicySettings? settings = await db.LockoutPolicySettings.FirstOrDefaultAsync(ct);

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
                    UpdatedAtUtc = DateTime.UtcNow,
                    UpdatedByOperatorId = null
                };
            }

            return settings;
        }

        public async Task UpsertAsync(LockoutPolicySettings settings, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);

            LockoutPolicySettings? existing = await db.LockoutPolicySettings.FirstOrDefaultAsync(ct);

            if (existing == null)
            {
                // Ensure a valid Id for insertion
                if (settings.Id == Guid.Empty)
                {
                    settings.Id = Guid.NewGuid();
                }
                settings.UpdatedAtUtc = DateTime.UtcNow;

                _ = db.LockoutPolicySettings.Add(settings);
                _logger.LogInformation("Lockout policy settings created");
            }
            else
            {
                // Update existing row
                existing.MaxFailedAttempts = settings.MaxFailedAttempts;
                existing.LockoutDurationMinutes = settings.LockoutDurationMinutes;
                existing.BlockedCountryCodes = settings.BlockedCountryCodes;
                existing.FailClosedOnLookupError = settings.FailClosedOnLookupError;
                existing.UpdatedAtUtc = DateTime.UtcNow;
                existing.UpdatedByOperatorId = settings.UpdatedByOperatorId;

                _logger.LogInformation("Lockout policy settings updated");
            }

            _ = await db.SaveChangesAsync(ct);
        }
    }
}
