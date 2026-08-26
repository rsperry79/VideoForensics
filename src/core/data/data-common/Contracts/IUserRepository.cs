using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Repository for user entities.</summary>
    public interface IUserRepository
    {
        /// <summary>Gets a user by ID.</summary>
        Task<User?> GetAsync(Guid userId, CancellationToken ct);

        /// <summary>Gets a user by provider key.</summary>
        Task<User?> GetByProviderKeyAsync(string providerUserKey, CancellationToken ct);

        /// <summary>Lists all users.</summary>
        Task<IReadOnlyList<User>> ListAsync(CancellationToken ct);

        /// <summary>Adds a new user.</summary>
        Task AddAsync(User user, CancellationToken ct);

        /// <summary>Updates an existing user.</summary>
        Task UpdateAsync(User user, CancellationToken ct);

        /// <summary>Deletes a user.</summary>
        Task DeleteAsync(Guid userId, CancellationToken ct);
    }
}
