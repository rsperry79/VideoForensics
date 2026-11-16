using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Core.Api.Contracts.Repositories
{
    /// <summary>Server-side <see cref="IProviderAccountRepository"/> over <see cref="IProviderAccountWriteRepository"/>, mapping to DTOs.</summary>
    public sealed class ProviderAccountDtoRepository(IProviderAccountWriteRepository accounts) : IProviderAccountRepository
    {
        /// <inheritdoc />
        public async Task<ProviderAccountDto?> GetAsync(Guid accountId, CancellationToken ct) =>
            (await accounts.GetAsync(accountId, ct))?.ToDto();

        /// <inheritdoc />
        public async Task<IReadOnlyList<ProviderAccountDto>> GetByUserIdAsync(Guid userId, CancellationToken ct) =>
            (await accounts.GetByUserIdAsync(userId, ct)).Select(a => a.ToDto()).ToList();

        /// <inheritdoc />
        public async Task<ProviderAccountDto?> GetByUserAndProviderAsync(Guid userId, string providerName, CancellationToken ct) =>
            (await accounts.GetByUserAndProviderAsync(userId, providerName, ct))?.ToDto();

        /// <inheritdoc />
        public async Task<IReadOnlyList<ProviderAccountDto>> ListAsync(CancellationToken ct) =>
            (await accounts.ListAsync(ct)).Select(a => a.ToDto()).ToList();

        /// <inheritdoc />
        public async Task<IReadOnlyList<ProviderAccountDto>> ListActiveAsync(CancellationToken ct) =>
            (await accounts.ListActiveAsync(ct)).Select(a => a.ToDto()).ToList();

        /// <inheritdoc />
        public async Task<IReadOnlyList<ProviderAccountDto>> ListRemovedAsync(CancellationToken ct) =>
            (await accounts.ListRemovedAsync(ct)).Select(a => a.ToDto()).ToList();

        /// <inheritdoc />
        public Task RetireAsync(Guid accountId, DateTime removedUtc, CancellationToken ct, string? stepUpToken = null) =>
            accounts.RetireAsync(accountId, removedUtc, ct, stepUpToken);

        /// <inheritdoc />
        public Task AddAsync(ProviderAccountDto account, CancellationToken ct) => accounts.AddAsync(account.ToDomain(), ct);

        /// <inheritdoc />
        public Task UpdateAsync(ProviderAccountDto account, CancellationToken ct) => accounts.UpdateAsync(account.ToDomain(), ct);

        /// <inheritdoc />
        public Task DeleteAsync(Guid accountId, CancellationToken ct, string? stepUpToken = null) =>
            accounts.DeleteAsync(accountId, ct, stepUpToken);

        /// <inheritdoc />
        public Task RecordErrorAsync(Guid providerAccountId, string errorMessage, CancellationToken cancellationToken) =>
            accounts.RecordErrorAsync(providerAccountId, errorMessage, cancellationToken);

        /// <inheritdoc />
        public Task RecordSuccessAsync(Guid providerAccountId, CancellationToken cancellationToken) =>
            accounts.RecordSuccessAsync(providerAccountId, cancellationToken);
    }
}
