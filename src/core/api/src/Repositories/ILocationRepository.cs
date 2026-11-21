namespace VideoForensics.Core.Api.Contracts.Repositories
{
    /// <summary>Location reads and commands, with DTO signatures. Client hosts use this interface.</summary>
    public interface ILocationRepository
    {
        /// <summary>Gets a location by id, or null.</summary>
        Task<LocationRecordDto?> GetAsync(Guid locationId, CancellationToken ct);

        /// <summary>Locations linked to a provider account.</summary>
        Task<IReadOnlyList<LocationRecordDto>> ListByProviderAccountIdAsync(Guid providerAccountId, CancellationToken ct);

        /// <summary>Provider accounts linked to a location.</summary>
        Task<IReadOnlyList<ProviderAccountDto>> ListProviderAccountsForLocationAsync(Guid locationId, CancellationToken ct);

        /// <summary>Links a location to a provider account.</summary>
        Task LinkToProviderAccountAsync(Guid locationId, Guid providerAccountId, CancellationToken ct);

        /// <summary>Gets a location by its provider location id, or null.</summary>
        Task<LocationRecordDto?> GetByProviderLocationIdAsync(string providerLocationId, CancellationToken ct);

        /// <summary>Gets a location by account id and provider location id, or null.</summary>
        Task<LocationRecordDto?> GetByProviderLocationIdAsync(Guid accountId, string providerLocationId, CancellationToken ct);

        /// <summary>Lists all locations.</summary>
        Task<IReadOnlyList<LocationRecordDto>> ListAsync(CancellationToken ct);

        /// <summary>Adds a location.</summary>
        Task AddAsync(LocationRecordDto location, CancellationToken ct);

        /// <summary>Updates a location.</summary>
        Task UpdateAsync(LocationRecordDto location, CancellationToken ct);

        /// <summary>Deletes a location.</summary>
        Task DeleteAsync(Guid locationId, CancellationToken ct);
    }
}
