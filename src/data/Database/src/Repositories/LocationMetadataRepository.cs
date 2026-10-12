using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Repository implementation for location metadata.</summary>
    public class LocationMetadataRepository : ILocationMetadataRepository
    {
        private readonly IDbContextFactory<VideoForensicsDbContext> _factory;
        private readonly ILogger<LocationMetadataRepository> _logger;

        public LocationMetadataRepository(IDbContextFactory<VideoForensicsDbContext> factory, ILogger<LocationMetadataRepository> logger)
        {
            _factory = factory;
            _logger = logger;
        }

        public async Task<LocationMetadata?> GetAsync(Guid id, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await db.LocationMetadata.FirstOrDefaultAsync(m => m.Id == id, ct);
        }

        public async Task<LocationMetadata?> GetByLocationIdAsync(Guid locationId, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await db.LocationMetadata.FirstOrDefaultAsync(m => m.LocationId == locationId, ct);
        }

        public async Task<LocationMetadata?> GetByApiHashAsync(string apiResponseHash, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await db.LocationMetadata.FirstOrDefaultAsync(m => m.ApiResponseHash == apiResponseHash, ct);
        }

        public async Task AddAsync(LocationMetadata metadata, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            try
            {
                _ = db.LocationMetadata.Add(metadata);
                _ = await db.SaveChangesAsync(ct);
                _logger.LogInformation("Location metadata added for location {LocationId}", metadata.LocationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding location metadata for location {LocationId}", metadata.LocationId);
                throw;
            }
        }

        public async Task UpdateAsync(LocationMetadata metadata, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            try
            {
                _ = db.LocationMetadata.Update(metadata);
                _ = await db.SaveChangesAsync(ct);
                _logger.LogInformation("Location metadata updated for location {LocationId}", metadata.LocationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating location metadata for location {LocationId}", metadata.LocationId);
                throw;
            }
        }

        public async Task DeleteAsync(Guid id, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            try
            {
                LocationMetadata? metadata = await db.LocationMetadata.FirstOrDefaultAsync(m => m.Id == id, ct);
                if (metadata != null)
                {
                    _ = db.LocationMetadata.Remove(metadata);
                    _ = await db.SaveChangesAsync(ct);
                    _logger.LogInformation("Location metadata deleted: {MetadataId}", id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting location metadata: {MetadataId}", id);
                throw;
            }
        }
    }
}
