using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Repository implementation for TwoFactorRoleRequirement entities (per-role two-factor authentication requirement).</summary>
    public class TwoFactorRoleRequirementRepository : ITwoFactorRoleRequirementRepository
    {
        private readonly IDbContextFactory<VideoForensicsDbContext> _factory;
        private readonly ILogger<TwoFactorRoleRequirementRepository> _logger;

        public TwoFactorRoleRequirementRepository(IDbContextFactory<VideoForensicsDbContext> factory, ILogger<TwoFactorRoleRequirementRepository> logger)
        {
            _factory = factory;
            _logger = logger;
        }

        public async Task<IReadOnlyList<TwoFactorRoleRequirement>> GetAllAsync(CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            List<TwoFactorRoleRequirement> requirements = await db.TwoFactorRoleRequirements.ToListAsync(ct);

            // If table is empty, synthesize defaults for all four roles with RequireTwoFactor=true (secure-by-default)
            if (requirements.Count == 0)
            {
                var allRoles = new[] { OperatorRole.ReadOnly, OperatorRole.Review, OperatorRole.Admin, OperatorRole.SuperAdmin };
                requirements = allRoles.Select(role => new TwoFactorRoleRequirement
                {
                    Id = Guid.NewGuid(),
                    Role = role,
                    RequireTwoFactor = true,
                    UpdatedAtUtc = DateTime.UtcNow,
                    UpdatedByOperatorId = null
                }).ToList();
            }

            return requirements.AsReadOnly();
        }

        public async Task<bool> GetRequirementForRoleAsync(OperatorRole role, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            TwoFactorRoleRequirement? requirement = await db.TwoFactorRoleRequirements
                .FirstOrDefaultAsync(r => r.Role == role, ct);

            // Fall back to true (secure-by-default) if no explicit row exists
            return requirement?.RequireTwoFactor ?? true;
        }

        public async Task UpsertAsync(TwoFactorRoleRequirement requirement, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);

            TwoFactorRoleRequirement? existing = await db.TwoFactorRoleRequirements
                .FirstOrDefaultAsync(r => r.Role == requirement.Role, ct);

            if (existing == null)
            {
                // Insert new row
                _ = db.TwoFactorRoleRequirements.Add(requirement);
                _logger.LogInformation("Two-factor role requirement created: {Role}", requirement.Role);
            }
            else
            {
                // Update existing row
                existing.RequireTwoFactor = requirement.RequireTwoFactor;
                existing.UpdatedAtUtc = DateTime.UtcNow;
                existing.UpdatedByOperatorId = requirement.UpdatedByOperatorId;

                _logger.LogInformation("Two-factor role requirement updated: {Role}", requirement.Role);
            }

            _ = await db.SaveChangesAsync(ct);
        }
    }
}
