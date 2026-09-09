using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Repository implementation for Device entities.</summary>
    public class DeviceRepository : IDeviceRepository
    {
        private readonly IDbContextFactory<VideoForensicsDbContext> _factory;
        private readonly ILogger<DeviceRepository> _logger;

        /// <summary>Initializes a new instance of the DeviceRepository.</summary>
        public DeviceRepository(IDbContextFactory<VideoForensicsDbContext> factory, ILogger<DeviceRepository> logger)
        {
            _factory = factory;
            _logger = logger;
        }

        /// <summary>Gets a device by ID.</summary>
        public async Task<Device?> GetAsync(Guid deviceId, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await db.Devices.FirstOrDefaultAsync(d => d.Id == deviceId, ct);
        }

        /// <summary>Gets all devices for a location.</summary>
        public async Task<IReadOnlyList<Device>> GetByLocationIdAsync(Guid locationId, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await db.Devices.Where(d => d.LocationId == locationId).ToListAsync(ct);
        }

        /// <summary>Gets a device by location ID and provider device ID.</summary>
        public async Task<Device?> GetByProviderDeviceIdAsync(Guid locationId, string providerDeviceId, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await db.Devices.FirstOrDefaultAsync(
                d => d.LocationId == locationId && d.ProviderDeviceId == providerDeviceId, ct);
        }

        /// <summary>Gets a device by API response hash for deduplication.</summary>
        public async Task<Device?> GetByApiHashAsync(string apiResponseHash, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await db.Devices.FirstOrDefaultAsync(d => d.ApiResponseHash == apiResponseHash, ct);
        }

        /// <summary>Lists all devices.</summary>
        public async Task<IReadOnlyList<Device>> ListAsync(CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await db.Devices.ToListAsync(ct);
        }

        /// <summary>Adds a new device.</summary>
        public async Task AddAsync(Device device, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            try
            {
                _ = db.Devices.Add(device);
                _ = await db.SaveChangesAsync(ct);
                _logger.LogInformation("Device added: {DeviceId} ({DeviceName})", device.Id, device.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding device: {DeviceName}", device.Name);
                throw;
            }
        }

        /// <summary>Updates an existing device.</summary>
        public async Task UpdateAsync(Device device, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            try
            {
                _ = db.Devices.Update(device);
                _ = await db.SaveChangesAsync(ct);
                _logger.LogInformation("Device updated: {DeviceId}", device.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating device: {DeviceId}", device.Id);
                throw;
            }
        }

        /// <summary>Updates the last successful pull timestamp for a device.</summary>
        public async Task UpdateLastSuccessfulPullAsync(Guid deviceId, DateTime pulledAtUtc, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            try
            {
                Device? device = await db.Devices.FirstOrDefaultAsync(d => d.Id == deviceId, ct);
                if (device != null)
                {
                    device.LastSuccessfulPullAtUtc = pulledAtUtc;
                    _ = await db.SaveChangesAsync(ct);
                    _logger.LogInformation("Device watermark updated: {DeviceId} ({PulledAtUtc})", deviceId, pulledAtUtc);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating device watermark: {DeviceId}", deviceId);
                throw;
            }
        }

        /// <summary>Deletes a device.</summary>
        public async Task DeleteAsync(Guid deviceId, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            try
            {
                Device? device = await db.Devices.FirstOrDefaultAsync(d => d.Id == deviceId, ct);
                if (device != null)
                {
                    _ = db.Devices.Remove(device);
                    _ = await db.SaveChangesAsync(ct);
                    _logger.LogInformation("Device deleted: {DeviceId}", deviceId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting device: {DeviceId}", deviceId);
                throw;
            }
        }
    }
}
