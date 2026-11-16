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
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<LocationMetadataRepository> _logger;

        public LocationMetadataRepository(IUnitOfWork unitOfWork, ILogger<LocationMetadataRepository> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public Task<LocationMetadata?> GetAsync(Guid id, CancellationToken ct)
        {
            return _unitOfWork.ReadAsync(context => context.Db().LocationMetadata.FirstOrDefaultAsync(m => m.Id == id, ct), ct);
        }

        public Task<LocationMetadata?> GetByLocationIdAsync(Guid locationId, CancellationToken ct)
        {
            return _unitOfWork.ReadAsync(context => context.Db().LocationMetadata.FirstOrDefaultAsync(m => m.LocationId == locationId, ct), ct);
        }

        public async Task AddAsync(LocationMetadata metadata, CancellationToken ct)
        {
            try
            {
                _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
                {
                    VideoForensicsDbContext db = context.Db();
                    _ = db.LocationMetadata.Add(metadata);
                    _ = await db.SaveChangesAsync(ct);
                    return null;
                }, ct);
                _logger.LogDebug("Location metadata added for location {LocationId}", metadata.LocationId);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error adding location metadata for location {LocationId}: {Message} ({ErrorType})", metadata.LocationId, ex.Message, ex.GetType().Name);
                throw;
            }
        }

        public async Task UpdateAsync(LocationMetadata metadata, CancellationToken ct)
        {
            try
            {
                _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
                {
                    VideoForensicsDbContext db = context.Db();
                    _ = db.LocationMetadata.Update(metadata);
                    _ = await db.SaveChangesAsync(ct);
                    return null;
                }, ct);
                _logger.LogDebug("Location metadata updated for location {LocationId}", metadata.LocationId);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error updating location metadata for location {LocationId}: {Message} ({ErrorType})", metadata.LocationId, ex.Message, ex.GetType().Name);
                throw;
            }
        }

        public async Task DeleteAsync(Guid id, CancellationToken ct)
        {
            try
            {
                bool deleted = await _unitOfWork.ExecuteAsync(async context =>
                {
                    VideoForensicsDbContext db = context.Db();
                    LocationMetadata? metadata = await db.LocationMetadata.FirstOrDefaultAsync(m => m.Id == id, ct);
                    if (metadata == null)
                    {
                        return false;
                    }

                    _ = db.LocationMetadata.Remove(metadata);
                    _ = await db.SaveChangesAsync(ct);
                    return true;
                }, ct);

                if (deleted)
                {
                    _logger.LogDebug("Location metadata deleted: {MetadataId}", id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error deleting location metadata: {MetadataId}: {Message} ({ErrorType})", id, ex.Message, ex.GetType().Name);
                throw;
            }
        }
    }
}
