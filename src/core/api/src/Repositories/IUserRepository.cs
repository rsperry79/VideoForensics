namespace VideoForensics.Core.Api.Contracts.Repositories
{
    /// <summary>User accounts, with DTO signatures. Client hosts use this interface; the server implements it.</summary>
    public interface IUserRepository
    {
        /// <summary>Gets a user by id, or null.</summary>
        Task<UserDto?> GetAsync(Guid userId, CancellationToken ct);

        /// <summary>Gets a user by provider user key, or null.</summary>
        Task<UserDto?> GetByProviderKeyAsync(string providerUserKey, CancellationToken ct);

        /// <summary>Lists all users.</summary>
        Task<IReadOnlyList<UserDto>> ListAsync(CancellationToken ct);

        /// <summary>Adds a user.</summary>
        Task AddAsync(UserDto user, CancellationToken ct);

        /// <summary>Updates a user.</summary>
        Task UpdateAsync(UserDto user, CancellationToken ct);

        /// <summary>Deletes a user.</summary>
        Task DeleteAsync(Guid userId, CancellationToken ct);
    }
}
