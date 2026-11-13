using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Repository implementation for device capabilities.</summary>
    public class DeviceCapabilitiesRepository : IDeviceCapabilitiesRepository
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<DeviceCapabilitiesRepository> _logger;

        public DeviceCapabilitiesRepository(IUnitOfWork unitOfWork, ILogger<DeviceCapabilitiesRepository> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public Task<DeviceCapabilities?> GetAsync(Guid id, CancellationToken ct)
        {
            return _unitOfWork.ReadAsync(context => context.Db().DeviceCapabilities.FirstOrDefaultAsync(c => c.Id == id, ct), ct);
        }

        public Task<DeviceCapabilities?> GetByDeviceIdAsync(Guid deviceId, CancellationToken ct)
        {
            return _unitOfWork.ReadAsync(context => context.Db().DeviceCapabilities.FirstOrDefaultAsync(c => c.DeviceId == deviceId, ct), ct);
        }

        public async Task AddAsync(DeviceCapabilities capabilities, CancellationToken ct)
        {
            try
            {
                _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
                {
                    VideoForensicsDbContext db = context.Db();
                    _ = db.DeviceCapabilities.Add(capabilities);
                    _ = await db.SaveChangesAsync(ct);
                    return null;
                }, ct);
                _logger.LogDebug("Device capabilities added for device {DeviceId}", capabilities.DeviceId);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error adding device capabilities for device {DeviceId}: {Message} ({ErrorType})", capabilities.DeviceId, ex.Message, ex.GetType().Name);
                throw;
            }
        }

        public async Task UpdateAsync(DeviceCapabilities capabilities, CancellationToken ct)
        {
            try
            {
                _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
                {
                    VideoForensicsDbContext db = context.Db();
                    _ = db.DeviceCapabilities.Update(capabilities);
                    _ = await db.SaveChangesAsync(ct);
                    return null;
                }, ct);
                _logger.LogDebug("Device capabilities updated for device {DeviceId}", capabilities.DeviceId);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error updating device capabilities for device {DeviceId}: {Message} ({ErrorType})", capabilities.DeviceId, ex.Message, ex.GetType().Name);
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
                    DeviceCapabilities? cap = await db.DeviceCapabilities.FirstOrDefaultAsync(c => c.Id == id, ct);
                    if (cap == null)
                    {
                        return false;
                    }

                    _ = db.DeviceCapabilities.Remove(cap);
                    _ = await db.SaveChangesAsync(ct);
                    return true;
                }, ct);

                if (deleted)
                {
                    _logger.LogDebug("Device capabilities deleted: {CapabilitiesId}", id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error deleting device capabilities: {CapabilitiesId}: {Message} ({ErrorType})", id, ex.Message, ex.GetType().Name);
                throw;
            }
        }
    }
}
