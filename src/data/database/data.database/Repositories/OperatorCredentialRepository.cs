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
        private readonly IDbContextFactory<VideoForensicsDbContext> _factory;
        private readonly ILogger<OperatorCredentialRepository> _logger;

        public OperatorCredentialRepository(IDbContextFactory<VideoForensicsDbContext> factory, ILogger<OperatorCredentialRepository> logger)
        {
            _factory = factory;
            _logger = logger;
        }

        public async Task<OperatorCredential?> GetAsync(Guid id, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await db.OperatorCredentials.FirstOrDefaultAsync(c => c.Id == id, ct);
        }

        public async Task<OperatorCredential?> GetByWebAuthnCredentialIdAsync(string credentialId, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await db.OperatorCredentials.FirstOrDefaultAsync(
                c => c.WebAuthnCredentialId == credentialId && c.RevokedAtUtc == null, ct);
        }

        public async Task<IReadOnlyList<OperatorCredential>> ListForOperatorAsync(Guid operatorId, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await db.OperatorCredentials
                .Where(c => c.OperatorId == operatorId)
                .OrderByDescending(c => c.CreatedAtUtc)
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<OperatorCredential>> ListPendingApprovalAsync(CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await db.OperatorCredentials
                .Where(c => c.IsApproved == false && c.RevokedAtUtc == null)
                .OrderBy(c => c.CreatedAtUtc)
                .ToListAsync(ct);
        }

        public async Task AddAsync(OperatorCredential credential, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            _ = db.OperatorCredentials.Add(credential);
            _ = await db.SaveChangesAsync(ct);
            _logger.LogInformation("Operator credential created: {CredentialId} for Operator {OperatorId}, label '{Label}'",
                credential.Id, credential.OperatorId, credential.Label);
        }

        public async Task ApproveAsync(Guid credentialId, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            OperatorCredential? credential = await db.OperatorCredentials.FirstOrDefaultAsync(c => c.Id == credentialId, ct);
            if (credential == null)
            {
                return;
            }

            credential.IsApproved = true;
            _ = await db.SaveChangesAsync(ct);
            _logger.LogInformation("Operator credential approved: {CredentialId}", credentialId);
        }

        public async Task RevokeAsync(Guid credentialId, string reason, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            OperatorCredential? credential = await db.OperatorCredentials.FirstOrDefaultAsync(c => c.Id == credentialId, ct);
            if (credential == null)
            {
                return;
            }

            credential.RevokedAtUtc = DateTime.UtcNow;
            credential.RevokedReason = reason;
            _ = await db.SaveChangesAsync(ct);
            _logger.LogWarning("Operator credential revoked: {CredentialId} - {Reason}", credentialId, SanitizeForLog(reason));
        }

        /// <summary>Sanitizes a string for logging by replacing newline characters to prevent log forging.</summary>
        private static string SanitizeForLog(string value) =>
            value.Replace('\r', '_').Replace('\n', '_');

        public async Task RecordSuccessfulAuthAsync(Guid credentialId, uint newSignCount, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            OperatorCredential? credential = await db.OperatorCredentials.FirstOrDefaultAsync(c => c.Id == credentialId, ct);
            if (credential == null)
            {
                return;
            }

            credential.WebAuthnSignCount = newSignCount;
            credential.LastUsedAtUtc = DateTime.UtcNow;
            _ = await db.SaveChangesAsync(ct);
        }

        public async Task MarkFirstLoginNotifiedAsync(Guid credentialId, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            OperatorCredential? credential = await db.OperatorCredentials.FirstOrDefaultAsync(c => c.Id == credentialId, ct);
            if (credential == null)
            {
                return;
            }

            credential.FirstLoginNotifiedAtUtc = DateTime.UtcNow;
            _ = await db.SaveChangesAsync(ct);
            _logger.LogInformation("Operator credential first login marked as notified: {CredentialId}", credentialId);
        }
    }
}
