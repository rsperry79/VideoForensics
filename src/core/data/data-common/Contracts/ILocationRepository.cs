using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Repository for location entities.</summary>
    public interface ILocationRepository
    {
        /// <summary>Gets a location by ID.</summary>
        Task<Location?> GetAsync(Guid locationId, CancellationToken ct);

        /// <summary>Gets all locations for a provider account.</summary>
        Task<IReadOnlyList<Location>> GetByProviderAccountIdAsync(Guid accountId, CancellationToken ct);

        /// <summary>Gets a location by provider account ID and provider location ID.</summary>
        Task<Location?> GetByProviderLocationIdAsync(Guid accountId, string providerLocationId, CancellationToken ct);

        /// <summary>Lists all locations.</summary>
        Task<IReadOnlyList<Location>> ListAsync(CancellationToken ct);

        /// <summary>Adds a new location.</summary>
        Task AddAsync(Location location, CancellationToken ct);

        /// <summary>Updates an existing location.</summary>
        Task UpdateAsync(Location location, CancellationToken ct);

        /// <summary>Deletes a location.</summary>
        Task DeleteAsync(Guid locationId, CancellationToken ct);
    }
}
