using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Core.Api.Contracts.Repositories
{
    /// <summary>Server-side <see cref="IUserRepository"/> over <see cref="IUserWriteRepository"/>, mapping to DTOs.</summary>
    public sealed class UserDtoRepository(IUserWriteRepository users) : IUserRepository
    {
        /// <inheritdoc />
        public async Task<UserDto?> GetAsync(Guid userId, CancellationToken ct) =>
            (await users.GetAsync(userId, ct))?.ToDto();

        /// <inheritdoc />
        public async Task<UserDto?> GetByProviderKeyAsync(string providerUserKey, CancellationToken ct) =>
            (await users.GetByProviderKeyAsync(providerUserKey, ct))?.ToDto();

        /// <inheritdoc />
        public async Task<IReadOnlyList<UserDto>> ListAsync(CancellationToken ct) =>
            (await users.ListAsync(ct)).Select(u => u.ToDto()).ToList();

        /// <inheritdoc />
        public Task AddAsync(UserDto user, CancellationToken ct) => users.AddAsync(user.ToDomain(), ct);

        /// <inheritdoc />
        public Task UpdateAsync(UserDto user, CancellationToken ct) => users.UpdateAsync(user.ToDomain(), ct);

        /// <inheritdoc />
        public Task DeleteAsync(Guid userId, CancellationToken ct) => users.DeleteAsync(userId, ct);
    }
}
