using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Repository for provider account entities.</summary>
    public interface IProviderAccountRepository
    {
        /// <summary>Gets a provider account by ID.</summary>
        Task<ProviderAccount?> GetAsync(Guid accountId, CancellationToken ct);

        /// <summary>Gets all provider accounts for a user.</summary>
        Task<IReadOnlyList<ProviderAccount>> GetByUserIdAsync(Guid userId, CancellationToken ct);

        /// <summary>Gets a provider account by user ID and provider name.</summary>
        Task<ProviderAccount?> GetByUserAndProviderAsync(Guid userId, string providerName, CancellationToken ct);

        /// <summary>Lists all provider accounts.</summary>
        Task<IReadOnlyList<ProviderAccount>> ListAsync(CancellationToken ct);

        /// <summary>Lists active provider accounts.</summary>
        Task<IReadOnlyList<ProviderAccount>> ListActiveAsync(CancellationToken ct);

        /// <summary>Adds a new provider account.</summary>
        Task AddAsync(ProviderAccount account, CancellationToken ct);

        /// <summary>Updates an existing provider account.</summary>
        Task UpdateAsync(ProviderAccount account, CancellationToken ct);

        /// <summary>Deletes a provider account.</summary>
        Task DeleteAsync(Guid accountId, CancellationToken ct);

        /// <summary>Records an error condition for a provider account.</summary>
        /// <param name="providerAccountId">The provider account ID.</param>
        /// <param name="errorMessage">The error message describing the failure condition.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task RecordErrorAsync(Guid providerAccountId, string errorMessage, CancellationToken cancellationToken);

        /// <summary>Records a successful authentication for a provider account, clearing any prior error state.</summary>
        /// <param name="providerAccountId">The provider account ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task RecordSuccessAsync(Guid providerAccountId, CancellationToken cancellationToken);
    }
}
