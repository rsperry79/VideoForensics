using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Repository implementation for Operator entities.</summary>
    public class OperatorRepository : IOperatorRepository
    {
        private readonly IDbContextFactory<VideoForensicsDbContext> _factory;
        private readonly ILogger<OperatorRepository> _logger;

        public OperatorRepository(IDbContextFactory<VideoForensicsDbContext> factory, ILogger<OperatorRepository> logger)
        {
            _factory = factory;
            _logger = logger;
        }

        public async Task<Operator?> GetAsync(Guid operatorId, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await db.Operators.FirstOrDefaultAsync(o => o.Id == operatorId, ct);
        }

        public async Task<Operator> AddAsync(Operator @operator, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            _ = db.Operators.Add(@operator);
            _ = await db.SaveChangesAsync(ct);
            _logger.LogInformation("Operator created: {OperatorId} ({DisplayName})", @operator.Id, SanitizeForLog(@operator.DisplayName));
            return @operator;
        }

        public async Task<IReadOnlyList<Operator>> ListAsync(CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await db.Operators.OrderBy(o => o.DisplayName).ToListAsync(ct);
        }

        public async Task<bool> IsEmptyAsync(CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return !await db.Operators.AnyAsync(ct);
        }

        public async Task DeactivateAsync(Guid operatorId, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            Operator? op = await db.Operators.FirstOrDefaultAsync(o => o.Id == operatorId, ct);
            if (op == null)
            {
                return;
            }

            op.Active = false;
            _ = await db.SaveChangesAsync(ct);
            _logger.LogInformation("Operator deactivated: {OperatorId}", operatorId);
        }

        public async Task ApproveAsync(Guid operatorId, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            Operator? op = await db.Operators.FirstOrDefaultAsync(o => o.Id == operatorId, ct);
            if (op == null)
            {
                return;
            }

            op.IsApproved = true;
            _ = await db.SaveChangesAsync(ct);
            _logger.LogInformation("Operator approved: {OperatorId}", operatorId);
        }

        public async Task UpdateDisplayNameAsync(Guid operatorId, string displayName, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            Operator? op = await db.Operators.FirstOrDefaultAsync(o => o.Id == operatorId, ct);
            if (op == null)
            {
                return;
            }

            op.DisplayName = displayName;
            _ = await db.SaveChangesAsync(ct);
            _logger.LogInformation("Operator display name updated: {OperatorId}", operatorId);
        }

        public async Task<Operator?> GetByUsernameAsync(string username, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await db.Operators.FirstOrDefaultAsync(o => o.Username == username, ct);
        }

        public async Task SetPasswordAsync(Guid operatorId, string passwordHash, bool mustChangePassword, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            Operator? op = await db.Operators.FirstOrDefaultAsync(o => o.Id == operatorId, ct);
            if (op == null)
            {
                return;
            }

            op.PasswordHash = passwordHash;
            op.MustChangePassword = mustChangePassword;
            op.PasswordUpdatedAtUtc = DateTime.UtcNow;
            op.SecurityStamp = Guid.NewGuid();
            _ = await db.SaveChangesAsync(ct);
            _logger.LogInformation("Operator password updated: {OperatorId}", operatorId);
        }

        public async Task SetRoleAsync(Guid operatorId, OperatorRole role, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            Operator? op = await db.Operators.FirstOrDefaultAsync(o => o.Id == operatorId, ct);
            if (op == null)
            {
                return;
            }

            op.Role = role;
            _ = await db.SaveChangesAsync(ct);
            _logger.LogInformation("Operator role updated: {OperatorId} - {Role}", operatorId, role);
        }

        public async Task SetApprovalFirstLoginNotifiedAsync(Guid operatorId, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            Operator? op = await db.Operators.FirstOrDefaultAsync(o => o.Id == operatorId, ct);
            if (op == null)
            {
                return;
            }

            op.ApprovalFirstLoginNotifiedAtUtc = DateTime.UtcNow;
            _ = await db.SaveChangesAsync(ct);
            _logger.LogInformation("Operator approval first login marked as notified: {OperatorId}", operatorId);
        }

        /// <summary>Sanitizes a string for logging by replacing newline characters to prevent log forging.</summary>
        private static string SanitizeForLog(string value) =>
            value.Replace('\r', '_').Replace('\n', '_');
    }
}
