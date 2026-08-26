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
        private readonly IDbContextFactory<VideoForensicsDbContext> _factory;
        private readonly ICredentialEncryptionProvider _encryptionProvider;
        private readonly ILogger<CredentialRepository> _logger;

        /// <summary>Initializes a new instance of the CredentialRepository.</summary>
        public CredentialRepository(
            IDbContextFactory<VideoForensicsDbContext> factory,
            ICredentialEncryptionProvider encryptionProvider,
            ILogger<CredentialRepository> logger)
        {
            _factory = factory;
            _encryptionProvider = encryptionProvider;
            _logger = logger;
        }

        /// <summary>Gets a credential, decrypting the value.</summary>
        public async Task<(string CredentialType, string DecryptedValue)?> GetAsync(
            Guid providerAccountId, string credentialType, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            var credential = await db.Credentials.FirstOrDefaultAsync(
                c => c.ProviderAccountId == providerAccountId && c.CredentialType == credentialType, ct);

            if (credential == null)
                return null;

            try
            {
                var decryptedValue = await _encryptionProvider.DecryptAsync(credential.EncryptedValue, ct);
                return (credential.CredentialType, decryptedValue);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error decrypting credential for account {ProviderAccountId}", providerAccountId);
                throw;
            }
        }

        /// <summary>Gets all credentials for a provider account.</summary>
        public async Task<IReadOnlyList<Credential>> GetByProviderAccountIdAsync(Guid providerAccountId, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            return await db.Credentials
                .Where(c => c.ProviderAccountId == providerAccountId)
                .ToListAsync(ct);
        }

        /// <summary>Sets or updates a credential, encrypting the value automatically.</summary>
        public async Task SetAsync(Guid providerAccountId, string credentialType, string plainValue, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            try
            {
                var encryptedValue = await _encryptionProvider.EncryptAsync(plainValue, ct);

                var credential = await db.Credentials.FirstOrDefaultAsync(
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
                    db.Credentials.Add(credential);
                }
                else
                {
                    credential.EncryptedValue = encryptedValue;
                    credential.RotatedUtc = DateTime.UtcNow;
                    db.Credentials.Update(credential);
                }

                await db.SaveChangesAsync(ct);
                _logger.LogInformation("Credential set for account {ProviderAccountId} (type: {CredentialType})",
                    providerAccountId, credentialType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting credential for account {ProviderAccountId}", providerAccountId);
                throw;
            }
        }

        /// <summary>Deletes a credential.</summary>
        public async Task DeleteAsync(Guid providerAccountId, string credentialType, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            try
            {
                var credential = await db.Credentials.FirstOrDefaultAsync(
                    c => c.ProviderAccountId == providerAccountId && c.CredentialType == credentialType, ct);

                if (credential != null)
                {
                    db.Credentials.Remove(credential);
                    await db.SaveChangesAsync(ct);
                    _logger.LogInformation("Credential deleted for account {ProviderAccountId} (type: {CredentialType})",
                        providerAccountId, credentialType);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting credential for account {ProviderAccountId}", providerAccountId);
                throw;
            }
        }

        /// <summary>Deletes all credentials for a provider account.</summary>
        public async Task DeleteByProviderAccountIdAsync(Guid providerAccountId, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            try
            {
                var credentials = await db.Credentials
                    .Where(c => c.ProviderAccountId == providerAccountId)
                    .ToListAsync(ct);

                if (credentials.Count > 0)
                {
                    db.Credentials.RemoveRange(credentials);
                    await db.SaveChangesAsync(ct);
                    _logger.LogInformation("All credentials deleted for account {ProviderAccountId}",
                        providerAccountId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting all credentials for account {ProviderAccountId}",
                    providerAccountId);
                throw;
            }
        }
    }
}
