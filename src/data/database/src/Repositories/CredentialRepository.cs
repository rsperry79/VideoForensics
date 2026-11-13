using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Repository implementation for Credential entities with automatic encryption/decryption.</summary>
    public class CredentialRepository : ICredentialRepository
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ICredentialEncryptionProvider _encryptionProvider;
        private readonly ILogger<CredentialRepository> _logger;

        /// <summary>Initializes a new instance of the CredentialRepository.</summary>
        public CredentialRepository(
            IUnitOfWork unitOfWork,
            ICredentialEncryptionProvider encryptionProvider,
            ILogger<CredentialRepository> logger)
        {
            _unitOfWork = unitOfWork;
            _encryptionProvider = encryptionProvider;
            _logger = logger;
        }

        /// <summary>Gets a credential, decrypting the value.</summary>
        public async Task<(string CredentialType, string DecryptedValue)?> GetAsync(
            Guid providerAccountId, string credentialType, CancellationToken ct)
        {
            return await _unitOfWork.ReadAsync<(string CredentialType, string DecryptedValue)?>(async context =>
            {
                VideoForensicsDbContext db = context.Db();
                Credential? credential = await db.Credentials.FirstOrDefaultAsync(
                    c => c.ProviderAccountId == providerAccountId && c.CredentialType == credentialType, ct);

                if (credential == null)
                {
                    return null;
                }

                try
                {
                    string decryptedValue = await _encryptionProvider.DecryptAsync(credential.EncryptedValue, ct);
                    return (credential.CredentialType, decryptedValue);
                }
                catch (Exception ex)
                {
                    ProviderAccount? account = await db.ProviderAccounts.FirstOrDefaultAsync(pa => pa.Id == providerAccountId, ct);
                    _logger.LogError("Error decrypting credential for {ProviderName} account: {Message} ({ErrorType})", account?.ProviderName ?? "unknown", ex.Message, ex.GetType().Name);
                    throw;
                }
            }, ct);
        }

        /// <summary>Gets all credentials for a provider account.</summary>
        public async Task<IReadOnlyList<Credential>> GetByProviderAccountIdAsync(Guid providerAccountId, CancellationToken ct)
        {
            return await _unitOfWork.ReadAsync(context => context.Db().Credentials
                .Where(c => c.ProviderAccountId == providerAccountId)
                .ToListAsync(ct), ct);
        }

        /// <summary>Sets or updates a credential, encrypting the value automatically.</summary>
        public async Task SetAsync(Guid providerAccountId, string credentialType, string plainValue, CancellationToken ct)
        {
            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                VideoForensicsDbContext db = context.Db();
                try
                {
                    string encryptedValue = await _encryptionProvider.EncryptAsync(plainValue, ct);

                    Credential? credential = await db.Credentials.FirstOrDefaultAsync(
                        c => c.ProviderAccountId == providerAccountId && c.CredentialType == credentialType, ct);

                    if (credential == null)
                    {
                        credential = new Credential
                        {
                            Id = Guid.NewGuid(),
                            ProviderAccountId = providerAccountId,
                            CredentialType = credentialType,
                            EncryptedValue = encryptedValue,
                            EncryptionProvider = "DataProtection",
                            CreatedUtc = DateTime.UtcNow
                        };
                        _ = db.Credentials.Add(credential);
                    }
                    else
                    {
                        credential.EncryptedValue = encryptedValue;
                        credential.RotatedUtc = DateTime.UtcNow;
                        _ = db.Credentials.Update(credential);
                    }

                    _ = await db.SaveChangesAsync(ct);
                    ProviderAccount? account = await db.ProviderAccounts.FirstOrDefaultAsync(pa => pa.Id == providerAccountId, ct);
                    _logger.LogDebug("Credential set for {ProviderName} account (type: {CredentialType})",
                        account?.ProviderName ?? "unknown", credentialType);
                }
                catch (Exception ex)
                {
                    ProviderAccount? account = await db.ProviderAccounts.FirstOrDefaultAsync(pa => pa.Id == providerAccountId, ct);
                    _logger.LogError("Error setting credential for {ProviderName} account: {Message} ({ErrorType})", account?.ProviderName ?? "unknown", ex.Message, ex.GetType().Name);
                    throw;
                }

                return null;
            }, ct);
        }

        /// <summary>Deletes a credential.</summary>
        public async Task DeleteAsync(Guid providerAccountId, string credentialType, CancellationToken ct)
        {
            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                VideoForensicsDbContext db = context.Db();
                try
                {
                    Credential? credential = await db.Credentials.FirstOrDefaultAsync(
                        c => c.ProviderAccountId == providerAccountId && c.CredentialType == credentialType, ct);

                    if (credential != null)
                    {
                        _ = db.Credentials.Remove(credential);
                        _ = await db.SaveChangesAsync(ct);
                        ProviderAccount? account = await db.ProviderAccounts.FirstOrDefaultAsync(pa => pa.Id == providerAccountId, ct);
                        _logger.LogDebug("Credential deleted for {ProviderName} account (type: {CredentialType})",
                            account?.ProviderName ?? "unknown", credentialType);
                    }
                }
                catch (Exception ex)
                {
                    ProviderAccount? account = await db.ProviderAccounts.FirstOrDefaultAsync(pa => pa.Id == providerAccountId, ct);
                    _logger.LogError("Error deleting credential for {ProviderName} account: {Message} ({ErrorType})", account?.ProviderName ?? "unknown", ex.Message, ex.GetType().Name);
                    throw;
                }

                return null;
            }, ct);
        }

        /// <summary>Deletes all credentials for a provider account.</summary>
        public async Task DeleteByProviderAccountIdAsync(Guid providerAccountId, CancellationToken ct)
        {
            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                VideoForensicsDbContext db = context.Db();
                try
                {
                    List<Credential> credentials = await db.Credentials
                        .Where(c => c.ProviderAccountId == providerAccountId)
                        .ToListAsync(ct);

                    if (credentials.Count > 0)
                    {
                        db.Credentials.RemoveRange(credentials);
                        _ = await db.SaveChangesAsync(ct);
                        ProviderAccount? account = await db.ProviderAccounts.FirstOrDefaultAsync(pa => pa.Id == providerAccountId, ct);
                        _logger.LogDebug("All credentials deleted for {ProviderName} account",
                            account?.ProviderName ?? "unknown");
                    }
                }
                catch (Exception ex)
                {
                    ProviderAccount? account = await db.ProviderAccounts.FirstOrDefaultAsync(pa => pa.Id == providerAccountId, ct);
                    _logger.LogError("Error deleting all credentials for {ProviderName} account: {Message} ({ErrorType})", account?.ProviderName ?? "unknown", ex.Message, ex.GetType().Name);
                    throw;
                }

                return null;
            }, ct);
        }
    }
}
