using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Repository implementation for Location entities.</summary>
    public class LocationRepository : ILocationRepository
    {
        private readonly IDbContextFactory<VideoForensicsDbContext> _factory;
        private readonly ILogger<LocationRepository> _logger;

        /// <summary>Initializes a new instance of the LocationRepository.</summary>
        public LocationRepository(IDbContextFactory<VideoForensicsDbContext> factory, ILogger<LocationRepository> logger)
        {
            _factory = factory;
            _logger = logger;
        }

        /// <summary>Gets a location by ID.</summary>
        public async Task<Location?> GetAsync(Guid locationId, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await db.Locations.FirstOrDefaultAsync(l => l.Id == locationId, ct);
        }

        /// <summary>Gets all locations for a provider account (deprecated - returns all locations for backward compatibility).</summary>
        [Obsolete("ProviderLocationId is now globally unique. Use ListAsync instead.")]
        public async Task<IReadOnlyList<Location>> GetByProviderAccountIdAsync(Guid accountId, CancellationToken ct)
        {
            // For backward compatibility, return all locations
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await db.Locations.ToListAsync(ct);
        }

        /// <summary>Gets a location by provider location ID (globally unique).</summary>
        public async Task<Location?> GetByProviderLocationIdAsync(string providerLocationId, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await db.Locations.FirstOrDefaultAsync(
                l => l.ProviderLocationId == providerLocationId, ct);
        }

        /// <summary>Gets a location by provider location ID (accountId parameter ignored for backward compatibility).</summary>
        public async Task<Location?> GetByProviderLocationIdAsync(Guid accountId, string providerLocationId, CancellationToken ct)
        {
            // accountId is ignored since ProviderLocationId is now globally unique
            return await GetByProviderLocationIdAsync(providerLocationId, ct);
        }

        /// <summary>Lists all locations.</summary>
        public async Task<IReadOnlyList<Location>> ListAsync(CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await db.Locations.ToListAsync(ct);
        }

        /// <summary>Adds a new location.</summary>
        public async Task AddAsync(Location location, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            try
            {
                _ = db.Locations.Add(location);
                _ = await db.SaveChangesAsync(ct);
                _logger.LogInformation("Location added: {LocationId} ({LocationName})", location.Id, location.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding location: {LocationName}", location.Name);
                throw;
            }
        }

        /// <summary>Updates an existing location.</summary>
        public async Task UpdateAsync(Location location, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            try
            {
                _ = db.Locations.Update(location);
                _ = await db.SaveChangesAsync(ct);
                _logger.LogInformation("Location updated: {LocationId}", location.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating location: {LocationId}", location.Id);
                throw;
            }
        }

        /// <summary>Deletes a location.</summary>
        public async Task DeleteAsync(Guid locationId, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            try
            {
                Location? location = await db.Locations.FirstOrDefaultAsync(l => l.Id == locationId, ct);
                if (location != null)
                {
                    _ = db.Locations.Remove(location);
                    _ = await db.SaveChangesAsync(ct);
                    _logger.LogInformation("Location deleted: {LocationId}", locationId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting location: {LocationId}", locationId);
                throw;
            }
        }
    }
}
