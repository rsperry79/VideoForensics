using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Repository implementation for device health records.</summary>
    public class DeviceHealthRepository : IDeviceHealthRepository
    {
        private readonly IDbContextFactory<VideoForensicsDbContext> _factory;
        private readonly ILogger<DeviceHealthRepository> _logger;

        public DeviceHealthRepository(IDbContextFactory<VideoForensicsDbContext> factory, ILogger<DeviceHealthRepository> logger)
        {
            _factory = factory;
            _logger = logger;
        }

        public async Task<DeviceHealth?> GetAsync(Guid id, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            return await db.DeviceHealthRecords.FirstOrDefaultAsync(h => h.Id == id, ct);
        }

        public async Task<DeviceHealth?> GetByDeviceIdAsync(Guid deviceId, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            return await db.DeviceHealthRecords.FirstOrDefaultAsync(h => h.DeviceId == deviceId, ct);
        }

        public async Task AddAsync(DeviceHealth health, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            try
            {
                db.DeviceHealthRecords.Add(health);
                await db.SaveChangesAsync(ct);
                _logger.LogInformation("Device health record added for device {DeviceId}", health.DeviceId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding device health for device {DeviceId}", health.DeviceId);
                throw;
            }
        }

        public async Task UpdateAsync(DeviceHealth health, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            try
            {
                db.DeviceHealthRecords.Update(health);
                await db.SaveChangesAsync(ct);
                _logger.LogInformation("Device health updated for device {DeviceId}", health.DeviceId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating device health for device {DeviceId}", health.DeviceId);
                throw;
            }
        }

        public async Task DeleteAsync(Guid id, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            try
            {
                var health = await db.DeviceHealthRecords.FirstOrDefaultAsync(h => h.Id == id, ct);
                if (health != null)
                {
                    db.DeviceHealthRecords.Remove(health);
                    await db.SaveChangesAsync(ct);
                    _logger.LogInformation("Device health record deleted: {HealthId}", id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting device health: {HealthId}", id);
                throw;
            }
        }
    }
}
