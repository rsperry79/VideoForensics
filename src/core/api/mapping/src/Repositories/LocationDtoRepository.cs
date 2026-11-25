using VideoForensics.Data.Common.Contracts;

namespace VideoForensics.Core.Api.Contracts.Repositories
{
    /// <summary>Server-side <see cref="ILocationRepository"/> over <see cref="ILocationWriteRepository"/>, mapping to DTOs.</summary>
    public sealed class LocationDtoRepository(ILocationWriteRepository locations) : ILocationRepository
    {
        /// <inheritdoc />
        public async Task<LocationRecordDto?> GetAsync(Guid locationId, CancellationToken ct) =>
            (await locations.GetAsync(locationId, ct))?.ToDto();

        /// <inheritdoc />
        public async Task<IReadOnlyList<LocationRecordDto>> ListByProviderAccountIdAsync(Guid providerAccountId, CancellationToken ct) =>
            (await locations.ListByProviderAccountIdAsync(providerAccountId, ct)).Select(l => l.ToDto()).ToList();

        /// <inheritdoc />
        public async Task<IReadOnlyList<ProviderAccountDto>> ListProviderAccountsForLocationAsync(Guid locationId, CancellationToken ct) =>
            (await locations.ListProviderAccountsForLocationAsync(locationId, ct)).Select(a => a.ToDto()).ToList();

        /// <inheritdoc />
        public Task LinkToProviderAccountAsync(Guid locationId, Guid providerAccountId, CancellationToken ct) =>
            locations.LinkToProviderAccountAsync(locationId, providerAccountId, ct);

        /// <inheritdoc />
        public async Task<LocationRecordDto?> GetByProviderLocationIdAsync(string providerLocationId, CancellationToken ct) =>
            (await locations.GetByProviderLocationIdAsync(providerLocationId, ct))?.ToDto();

        /// <inheritdoc />
        public async Task<LocationRecordDto?> GetByProviderLocationIdAsync(Guid accountId, string providerLocationId, CancellationToken ct) =>
            (await locations.GetByProviderLocationIdAsync(accountId, providerLocationId, ct))?.ToDto();

        /// <inheritdoc />
        public async Task<IReadOnlyList<LocationRecordDto>> ListAsync(CancellationToken ct) =>
            (await locations.ListAsync(ct)).Select(l => l.ToDto()).ToList();

        /// <inheritdoc />
        public Task AddAsync(LocationRecordDto location, CancellationToken ct) => locations.AddAsync(location.ToDomain(), ct);

        /// <inheritdoc />
        public Task UpdateAsync(LocationRecordDto location, CancellationToken ct) => locations.UpdateAsync(location.ToDomain(), ct);

        /// <inheritdoc />
        public Task DeleteAsync(Guid locationId, CancellationToken ct) => locations.DeleteAsync(locationId, ct);
    }
}
