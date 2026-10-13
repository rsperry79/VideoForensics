using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Repository for location entities.</summary>
    public interface ILocationRepository
    {
        /// <summary>Gets a location by ID.</summary>
        Task<Location?> GetAsync(Guid locationId, CancellationToken ct);

        /// <summary>Lists the locations linked to a provider account.</summary>
        Task<IReadOnlyList<Location>> ListByProviderAccountIdAsync(Guid providerAccountId, CancellationToken ct);

        /// <summary>Links a location to a provider account; no-op if the link already exists.</summary>
        Task LinkToProviderAccountAsync(Guid locationId, Guid providerAccountId, CancellationToken ct);

        /// <summary>Gets a location by provider location ID (now globally unique, accountId parameter ignored).</summary>
        Task<Location?> GetByProviderLocationIdAsync(string providerLocationId, CancellationToken ct);

        /// <summary>Gets a location by provider location ID. Overload for backward compatibility (accountId is ignored).</summary>
        Task<Location?> GetByProviderLocationIdAsync(Guid accountId, string providerLocationId, CancellationToken ct);

        /// <summary>Gets a location by API response hash for deduplication.</summary>
        Task<Location?> GetByApiHashAsync(string apiResponseHash, CancellationToken ct);

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
