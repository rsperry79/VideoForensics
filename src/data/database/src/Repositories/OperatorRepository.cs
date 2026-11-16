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
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<OperatorRepository> _logger;

        public OperatorRepository(IUnitOfWork unitOfWork, ILogger<OperatorRepository> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public Task<Operator?> GetAsync(Guid operatorId, CancellationToken ct)
        {
            return _unitOfWork.ReadAsync(context => context.Db().Operators.FirstOrDefaultAsync(o => o.Id == operatorId, ct), ct);
        }

        public async Task<Operator> AddAsync(Operator @operator, CancellationToken ct)
        {
            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                VideoForensicsDbContext db = context.Db();
                _ = db.Operators.Add(@operator);
                _ = await db.SaveChangesAsync(ct);
                return null;
            }, ct);
            _logger.LogDebug("Operator created: {DisplayName}", SanitizeForLog(@operator.DisplayName));
            return @operator;
        }

        public Task<IReadOnlyList<Operator>> ListAsync(CancellationToken ct)
        {
            return _unitOfWork.ReadAsync<IReadOnlyList<Operator>>(async context =>
            {
                return await context.Db().Operators.OrderBy(o => o.DisplayName).ToListAsync(ct);
            }, ct);
        }

        public Task<bool> IsEmptyAsync(CancellationToken ct)
        {
            return _unitOfWork.ReadAsync(async context => !await context.Db().Operators.AnyAsync(ct), ct);
        }

        public async Task DeactivateAsync(Guid operatorId, CancellationToken ct)
        {
            Operator? op = await _unitOfWork.ExecuteAsync(async context =>
            {
                VideoForensicsDbContext db = context.Db();
                Operator? found = await db.Operators.FirstOrDefaultAsync(o => o.Id == operatorId, ct);
                if (found == null)
                {
                    return found;
                }

                found.Active = false;
                _ = await db.SaveChangesAsync(ct);
                return found;
            }, ct);

            if (op == null)
            {
                return;
            }

            _logger.LogDebug("Operator deactivated: {DisplayName}", SanitizeForLog(op.DisplayName));
        }

        public async Task ReactivateAsync(Guid operatorId, CancellationToken ct)
        {
            Operator? op = await _unitOfWork.ExecuteAsync(async context =>
            {
                VideoForensicsDbContext db = context.Db();
                Operator? found = await db.Operators.FirstOrDefaultAsync(o => o.Id == operatorId, ct);
                if (found == null)
                {
                    return found;
                }

                found.Active = true;
                _ = await db.SaveChangesAsync(ct);
                return found;
            }, ct);

            if (op == null)
            {
                return;
            }

            _logger.LogDebug("Operator reactivated: {DisplayName}", SanitizeForLog(op.DisplayName));
        }

        public async Task ApproveAsync(Guid operatorId, CancellationToken ct)
        {
            Operator? op = await _unitOfWork.ExecuteAsync(async context =>
            {
                VideoForensicsDbContext db = context.Db();
                Operator? found = await db.Operators.FirstOrDefaultAsync(o => o.Id == operatorId, ct);
                if (found == null)
                {
                    return found;
                }

                found.IsApproved = true;
                _ = await db.SaveChangesAsync(ct);
                return found;
            }, ct);

            if (op == null)
            {
                return;
            }

            _logger.LogDebug("Operator approved: {DisplayName}", SanitizeForLog(op.DisplayName));
        }

        public async Task UpdateDisplayNameAsync(Guid operatorId, string displayName, CancellationToken ct)
        {
            Operator? op = await _unitOfWork.ExecuteAsync(async context =>
            {
                VideoForensicsDbContext db = context.Db();
                Operator? found = await db.Operators.FirstOrDefaultAsync(o => o.Id == operatorId, ct);
                if (found == null)
                {
                    return found;
                }

                found.DisplayName = displayName;
                _ = await db.SaveChangesAsync(ct);
                return found;
            }, ct);

            if (op == null)
            {
                return;
            }

            _logger.LogDebug("Operator display name updated: {DisplayName}", SanitizeForLog(op.DisplayName));
        }

        public Task<Operator?> GetByUsernameAsync(string username, CancellationToken ct)
        {
            return _unitOfWork.ReadAsync(context => context.Db().Operators.FirstOrDefaultAsync(o => o.Username == username, ct), ct);
        }

        public async Task SetPasswordAsync(Guid operatorId, string passwordHash, bool mustChangePassword, CancellationToken ct)
        {
            Operator? op = await _unitOfWork.ExecuteAsync(async context =>
            {
                VideoForensicsDbContext db = context.Db();
                Operator? found = await db.Operators.FirstOrDefaultAsync(o => o.Id == operatorId, ct);
                if (found == null)
                {
                    return found;
                }

                found.PasswordHash = passwordHash;
                found.MustChangePassword = mustChangePassword;
                found.PasswordUpdatedAtUtc = DateTime.UtcNow;
                found.SecurityStamp = Guid.NewGuid();
                _ = await db.SaveChangesAsync(ct);
                return found;
            }, ct);

            if (op == null)
            {
                return;
            }

            _logger.LogDebug("Operator password updated: {DisplayName}", SanitizeForLog(op.DisplayName));
        }

        public async Task SetRoleAsync(Guid operatorId, OperatorRole role, CancellationToken ct)
        {
            Operator? op = await _unitOfWork.ExecuteAsync(async context =>
            {
                VideoForensicsDbContext db = context.Db();
                Operator? found = await db.Operators.FirstOrDefaultAsync(o => o.Id == operatorId, ct);
                if (found == null)
                {
                    return found;
                }

                found.Role = role;
                _ = await db.SaveChangesAsync(ct);
                return found;
            }, ct);

            if (op == null)
            {
                return;
            }

            _logger.LogDebug("Operator role updated: {DisplayName} - {Role}", SanitizeForLog(op.DisplayName), role);
        }

        public async Task SetApprovalFirstLoginNotifiedAsync(Guid operatorId, CancellationToken ct)
        {
            Operator? op = await _unitOfWork.ExecuteAsync(async context =>
            {
                VideoForensicsDbContext db = context.Db();
                Operator? found = await db.Operators.FirstOrDefaultAsync(o => o.Id == operatorId, ct);
                if (found == null)
                {
                    return found;
                }

                found.ApprovalFirstLoginNotifiedAtUtc = DateTime.UtcNow;
                _ = await db.SaveChangesAsync(ct);
                return found;
            }, ct);

            if (op == null)
            {
                return;
            }

            _logger.LogDebug("Operator approval first login marked as notified: {DisplayName}", SanitizeForLog(op.DisplayName));
        }

        public async Task IncrementFailedLoginAttemptAsync(Guid operatorId, int maxFailedAttempts, int lockoutDurationMinutes, CancellationToken ct)
        {
            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                VideoForensicsDbContext db = context.Db();
                Operator? op = await db.Operators.FirstOrDefaultAsync(o => o.Id == operatorId, ct);
                if (op == null)
                {
                    return null;
                }

                op.FailedLoginAttemptCount++;
                if (op.FailedLoginAttemptCount >= maxFailedAttempts)
                {
                    op.LockedOutUntilUtc = DateTime.UtcNow.AddMinutes(lockoutDurationMinutes);
                    _logger.LogWarning("Operator account locked due to failed login attempts: {DisplayName}", SanitizeForLog(op.DisplayName));
                }

                _ = await db.SaveChangesAsync(ct);
                return null;
            }, ct);
        }

        public async Task ResetFailedLoginAttemptsAsync(Guid operatorId, CancellationToken ct)
        {
            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                VideoForensicsDbContext db = context.Db();
                Operator? op = await db.Operators.FirstOrDefaultAsync(o => o.Id == operatorId, ct);
                if (op == null)
                {
                    return null;
                }

                op.FailedLoginAttemptCount = 0;
                op.LockedOutUntilUtc = null;
                _ = await db.SaveChangesAsync(ct);
                return null;
            }, ct);
        }

        public async Task UnlockAsync(Guid operatorId, CancellationToken ct)
        {
            Operator? op = await _unitOfWork.ExecuteAsync(async context =>
            {
                VideoForensicsDbContext db = context.Db();
                Operator? found = await db.Operators.FirstOrDefaultAsync(o => o.Id == operatorId, ct);
                if (found == null)
                {
                    return found;
                }

                found.FailedLoginAttemptCount = 0;
                found.LockedOutUntilUtc = null;
                _ = await db.SaveChangesAsync(ct);
                return found;
            }, ct);

            if (op == null)
            {
                return;
            }

            _logger.LogDebug("Operator account manually unlocked: {DisplayName}", SanitizeForLog(op.DisplayName));
        }

        public async Task SetPasskeyRequiredAsync(Guid operatorId, bool passkeyRequired, CancellationToken ct)
        {
            Operator? op = await _unitOfWork.ExecuteAsync(async context =>
            {
                VideoForensicsDbContext db = context.Db();
                Operator? found = await db.Operators.FirstOrDefaultAsync(o => o.Id == operatorId, ct);
                if (found == null)
                {
                    return found;
                }

                found.PasskeyRequired = passkeyRequired;
                _ = await db.SaveChangesAsync(ct);
                return found;
            }, ct);

            if (op == null)
            {
                return;
            }

            _logger.LogDebug("Operator passkey requirement updated: {DisplayName} - passkey required {PasskeyRequired}", SanitizeForLog(op.DisplayName), passkeyRequired);
        }

        public async Task SetTwoFactorRequirementOverrideAsync(Guid operatorId, TwoFactorRequirementOverride value, CancellationToken ct)
        {
            Operator? op = await _unitOfWork.ExecuteAsync(async context =>
            {
                VideoForensicsDbContext db = context.Db();
                Operator? found = await db.Operators.FirstOrDefaultAsync(o => o.Id == operatorId, ct);
                if (found == null)
                {
                    return found;
                }

                found.TwoFactorRequirementOverride = value;
                _ = await db.SaveChangesAsync(ct);
                return found;
            }, ct);

            if (op == null)
            {
                return;
            }

            _logger.LogDebug("Operator two-factor requirement override updated: {DisplayName} - {Override}", SanitizeForLog(op.DisplayName), value);
        }

        /// <summary>Sanitizes a string for logging by replacing newline characters to prevent log forging.</summary>
        private static string SanitizeForLog(string value)
        {
            return value.Replace('\r', '_').Replace('\n', '_');
        }
    }
}
