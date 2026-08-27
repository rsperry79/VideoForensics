using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Repository for encrypted credential entities, keyed by (ProviderAccountId, CredentialType).</summary>
    public interface ICredentialRepository
    {
        /// <summary>Gets a credential, decrypting the value.</summary>
        Task<(string CredentialType, string DecryptedValue)?> GetAsync(Guid providerAccountId, string credentialType, CancellationToken ct);

        /// <summary>Gets all credentials for a provider account.</summary>
        Task<IReadOnlyList<Credential>> GetByProviderAccountIdAsync(Guid providerAccountId, CancellationToken ct);

        /// <summary>Sets or updates a credential, encrypting the value automatically.</summary>
        Task SetAsync(Guid providerAccountId, string credentialType, string plainValue, CancellationToken ct);

        /// <summary>Deletes a credential.</summary>
        Task DeleteAsync(Guid providerAccountId, string credentialType, CancellationToken ct);

        /// <summary>Deletes all credentials for a provider account.</summary>
        Task DeleteByProviderAccountIdAsync(Guid providerAccountId, CancellationToken ct);
    }
}
