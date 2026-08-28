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
        private readonly IDbContextFactory<VideoForensicsDbContext> _factory;
        private readonly ILogger<DeviceCapabilitiesRepository> _logger;

        public DeviceCapabilitiesRepository(IDbContextFactory<VideoForensicsDbContext> factory, ILogger<DeviceCapabilitiesRepository> logger)
        {
            _factory = factory;
            _logger = logger;
        }

        public async Task<DeviceCapabilities?> GetAsync(Guid id, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            return await db.DeviceCapabilities.FirstOrDefaultAsync(c => c.Id == id, ct);
        }

        public async Task<DeviceCapabilities?> GetByDeviceIdAsync(Guid deviceId, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            return await db.DeviceCapabilities.FirstOrDefaultAsync(c => c.DeviceId == deviceId, ct);
        }

        public async Task AddAsync(DeviceCapabilities capabilities, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            try
            {
                db.DeviceCapabilities.Add(capabilities);
                await db.SaveChangesAsync(ct);
                _logger.LogInformation("Device capabilities added for device {DeviceId}", capabilities.DeviceId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding device capabilities for device {DeviceId}", capabilities.DeviceId);
                throw;
            }
        }

        public async Task UpdateAsync(DeviceCapabilities capabilities, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            try
            {
                db.DeviceCapabilities.Update(capabilities);
                await db.SaveChangesAsync(ct);
                _logger.LogInformation("Device capabilities updated for device {DeviceId}", capabilities.DeviceId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating device capabilities for device {DeviceId}", capabilities.DeviceId);
                throw;
            }
        }

        public async Task DeleteAsync(Guid id, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            try
            {
                var cap = await db.DeviceCapabilities.FirstOrDefaultAsync(c => c.Id == id, ct);
                if (cap != null)
                {
                    db.DeviceCapabilities.Remove(cap);
                    await db.SaveChangesAsync(ct);
                    _logger.LogInformation("Device capabilities deleted: {CapabilitiesId}", id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting device capabilities: {CapabilitiesId}", id);
                throw;
            }
        }
    }
}
