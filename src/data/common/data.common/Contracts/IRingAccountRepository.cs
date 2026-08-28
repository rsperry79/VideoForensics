using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Repository for Ring account-level data.</summary>
    public interface IRingAccountRepository
    {
        /// <summary>Gets a Ring account by ID.</summary>
        Task<RingAccount?> GetAsync(Guid id, CancellationToken ct);

        /// <summary>Gets Ring account by provider account ID.</summary>
        Task<RingAccount?> GetByProviderAccountIdAsync(Guid providerAccountId, CancellationToken ct);

        /// <summary>Adds a new Ring account.</summary>
        Task AddAsync(RingAccount account, CancellationToken ct);

        /// <summary>Updates an existing Ring account.</summary>
        Task UpdateAsync(RingAccount account, CancellationToken ct);

        /// <summary>Deletes a Ring account.</summary>
        Task DeleteAsync(Guid id, CancellationToken ct);
    }
}
