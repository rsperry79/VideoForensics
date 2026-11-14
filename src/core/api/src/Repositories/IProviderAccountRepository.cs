namespace VideoForensics.Core.Api.Contracts.Repositories
{
    /// <summary>Provider account reads and commands, with DTO signatures. Client hosts use this interface.</summary>
    public interface IProviderAccountRepository
    {
        /// <summary>Gets an account by id, or null.</summary>
        Task<ProviderAccountDto?> GetAsync(Guid accountId, CancellationToken ct);

        /// <summary>Accounts belonging to a user.</summary>
        Task<IReadOnlyList<ProviderAccountDto>> GetByUserIdAsync(Guid userId, CancellationToken ct);

        /// <summary>The account a user has with a provider, or null.</summary>
        Task<ProviderAccountDto?> GetByUserAndProviderAsync(Guid userId, string providerName, CancellationToken ct);

        /// <summary>Lists all accounts.</summary>
        Task<IReadOnlyList<ProviderAccountDto>> ListAsync(CancellationToken ct);

        /// <summary>Lists accounts that are not retired.</summary>
        Task<IReadOnlyList<ProviderAccountDto>> ListActiveAsync(CancellationToken ct);

        /// <summary>Lists retired accounts.</summary>
        Task<IReadOnlyList<ProviderAccountDto>> ListRemovedAsync(CancellationToken ct);

        /// <summary>Retires an account at the given UTC time.</summary>
        Task RetireAsync(Guid accountId, DateTime removedUtc, CancellationToken ct, string? stepUpToken = null);

        /// <summary>Adds an account.</summary>
        Task AddAsync(ProviderAccountDto account, CancellationToken ct);

        /// <summary>Updates an account.</summary>
        Task UpdateAsync(ProviderAccountDto account, CancellationToken ct);

        /// <summary>Deletes an account.</summary>
        Task DeleteAsync(Guid accountId, CancellationToken ct, string? stepUpToken = null);

        /// <summary>Records a failed sync for an account.</summary>
        Task RecordErrorAsync(Guid providerAccountId, string errorMessage, CancellationToken cancellationToken);

        /// <summary>Records a successful sync for an account.</summary>
        Task RecordSuccessAsync(Guid providerAccountId, CancellationToken cancellationToken);
    }
}
