using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Repository implementation for OperatorCredential entities.</summary>
    public class OperatorCredentialRepository : IOperatorCredentialRepository
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<OperatorCredentialRepository> _logger;

        public OperatorCredentialRepository(IUnitOfWork unitOfWork, ILogger<OperatorCredentialRepository> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<OperatorCredential?> GetAsync(Guid id, CancellationToken ct)
        {
            return await _unitOfWork.ReadAsync(context => context.Db().OperatorCredentials.FirstOrDefaultAsync(c => c.Id == id, ct), ct);
        }

        public async Task<OperatorCredential?> GetByWebAuthnCredentialIdAsync(string credentialId, CancellationToken ct)
        {
            return await _unitOfWork.ReadAsync(context => context.Db().OperatorCredentials.FirstOrDefaultAsync(
                c => c.WebAuthnCredentialId == credentialId && c.RevokedAtUtc == null, ct), ct);
        }

        public async Task<IReadOnlyList<OperatorCredential>> ListForOperatorAsync(Guid operatorId, CancellationToken ct)
        {
            return await _unitOfWork.ReadAsync(context => context.Db().OperatorCredentials
                .Where(c => c.OperatorId == operatorId)
                .OrderByDescending(c => c.CreatedAtUtc)
                .ToListAsync(ct), ct);
        }

        public async Task<IReadOnlyList<OperatorCredential>> ListPendingApprovalAsync(CancellationToken ct)
        {
            return await _unitOfWork.ReadAsync(context => context.Db().OperatorCredentials
                .Where(c => c.IsApproved == false && c.RevokedAtUtc == null)
                .OrderBy(c => c.CreatedAtUtc)
                .ToListAsync(ct), ct);
        }

        public async Task AddAsync(OperatorCredential credential, CancellationToken ct)
        {
            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                VideoForensicsDbContext db = context.Db();
                _ = db.OperatorCredentials.Add(credential);
                _ = await db.SaveChangesAsync(ct);
                _logger.LogDebug("Operator credential created: {CredentialId} for Operator {OperatorId}, label '{Label}'",
                    credential.Id, credential.OperatorId, credential.Label);
                return null;
            }, ct);
        }

        public async Task ApproveAsync(Guid credentialId, CancellationToken ct)
        {
            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                VideoForensicsDbContext db = context.Db();
                OperatorCredential? credential = await db.OperatorCredentials.FirstOrDefaultAsync(c => c.Id == credentialId, ct);
                if (credential == null)
                {
                    return null;
                }

                credential.IsApproved = true;
                _ = await db.SaveChangesAsync(ct);
                _logger.LogDebug("Operator credential approved: {CredentialId}", credentialId);
                return null;
            }, ct);
        }

        public async Task RevokeAsync(Guid credentialId, string reason, CancellationToken ct)
        {
            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                VideoForensicsDbContext db = context.Db();
                OperatorCredential? credential = await db.OperatorCredentials.FirstOrDefaultAsync(c => c.Id == credentialId, ct);
                if (credential == null)
                {
                    return null;
                }

                credential.RevokedAtUtc = DateTime.UtcNow;
                credential.RevokedReason = reason;
                _ = await db.SaveChangesAsync(ct);
                _logger.LogInformation("Operator credential revoked: {CredentialId} - {Reason}", credentialId, SanitizeForLog(reason));
                return null;
            }, ct);
        }

        /// <summary>Sanitizes a string for logging by replacing newline characters to prevent log forging.</summary>
        private static string SanitizeForLog(string value)
        {
            return value.Replace('\r', '_').Replace('\n', '_');
        }

        public async Task RecordSuccessfulAuthAsync(Guid credentialId, uint newSignCount, CancellationToken ct)
        {
            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                VideoForensicsDbContext db = context.Db();
                OperatorCredential? credential = await db.OperatorCredentials.FirstOrDefaultAsync(c => c.Id == credentialId, ct);
                if (credential == null)
                {
                    return null;
                }

                credential.WebAuthnSignCount = newSignCount;
                credential.LastUsedAtUtc = DateTime.UtcNow;
                _ = await db.SaveChangesAsync(ct);
                return null;
            }, ct);
        }

        public async Task MarkFirstLoginNotifiedAsync(Guid credentialId, CancellationToken ct)
        {
            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                VideoForensicsDbContext db = context.Db();
                OperatorCredential? credential = await db.OperatorCredentials.FirstOrDefaultAsync(c => c.Id == credentialId, ct);
                if (credential == null)
                {
                    return null;
                }

                credential.FirstLoginNotifiedAtUtc = DateTime.UtcNow;
                _ = await db.SaveChangesAsync(ct);
                _logger.LogDebug("Operator credential first login marked as notified: {CredentialId}", credentialId);
                return null;
            }, ct);
        }
    }
}
