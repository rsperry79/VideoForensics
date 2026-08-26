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
            await using var db = await _factory.CreateDbContextAsync(ct);
            return await db.Locations.FirstOrDefaultAsync(l => l.Id == locationId, ct);
        }

        /// <summary>Gets all locations for a provider account.</summary>
        public async Task<IReadOnlyList<Location>> GetByProviderAccountIdAsync(Guid accountId, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            return await db.Locations.Where(l => l.ProviderAccountId == accountId).ToListAsync(ct);
        }

        /// <summary>Gets a location by provider account ID and provider location ID.</summary>
        public async Task<Location?> GetByProviderLocationIdAsync(Guid accountId, string providerLocationId, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            return await db.Locations.FirstOrDefaultAsync(
                l => l.ProviderAccountId == accountId && l.ProviderLocationId == providerLocationId, ct);
        }

        /// <summary>Lists all locations.</summary>
        public async Task<IReadOnlyList<Location>> ListAsync(CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            return await db.Locations.ToListAsync(ct);
        }

        /// <summary>Adds a new location.</summary>
        public async Task AddAsync(Location location, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            try
            {
                db.Locations.Add(location);
                await db.SaveChangesAsync(ct);
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
            await using var db = await _factory.CreateDbContextAsync(ct);
            try
            {
                db.Locations.Update(location);
                await db.SaveChangesAsync(ct);
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
            await using var db = await _factory.CreateDbContextAsync(ct);
            try
            {
                var location = await db.Locations.FirstOrDefaultAsync(l => l.Id == locationId, ct);
                if (location != null)
                {
                    db.Locations.Remove(location);
                    await db.SaveChangesAsync(ct);
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
