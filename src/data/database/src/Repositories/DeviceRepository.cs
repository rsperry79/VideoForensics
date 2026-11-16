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
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<DeviceRepository> _logger;

        /// <summary>Initializes a new instance of the DeviceRepository.</summary>
        public DeviceRepository(IUnitOfWork unitOfWork, ILogger<DeviceRepository> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        /// <summary>Gets a device by ID.</summary>
        public Task<Device?> GetAsync(Guid deviceId, CancellationToken ct)
        {
            return _unitOfWork.ReadAsync(context => context.Db().Devices.FirstOrDefaultAsync(d => d.Id == deviceId, ct), ct);
        }

        /// <summary>Gets all devices for a location.</summary>
        public async Task<IReadOnlyList<Device>> GetByLocationIdAsync(Guid locationId, CancellationToken ct)
        {
            return await _unitOfWork.ReadAsync(context => context.Db().Devices.Where(d => d.LocationId == locationId).ToListAsync(ct), ct);
        }

        /// <summary>Gets a device by location ID and provider device ID.</summary>
        public Task<Device?> GetByProviderDeviceIdAsync(Guid locationId, string providerDeviceId, CancellationToken ct)
        {
            return _unitOfWork.ReadAsync(
                context => context.Db().Devices.FirstOrDefaultAsync(
                    d => d.LocationId == locationId && d.ProviderDeviceId == providerDeviceId, ct), ct);
        }

        /// <summary>Lists all devices.</summary>
        public async Task<IReadOnlyList<Device>> ListAsync(CancellationToken ct)
        {
            return await _unitOfWork.ReadAsync<IReadOnlyList<Device>>(async context =>
            {
                VideoForensicsDbContext db = context.Db();
                List<Device> devices = await db.Devices.ToListAsync(ct);

                // One lookup for all locations rather than one per device.
                var links = (await db.ProviderAccountLocations
                    .AsNoTracking()
                    .Select(l => new { l.LocationId, l.ProviderAccount!.ProviderName })
                    .ToListAsync(ct))
                    .Select(x => (x.LocationId, x.ProviderName))
                    .ToList();
                DeviceProviderNames.Assign(devices, links);
                return devices;
            }, ct);
        }

        /// <summary>Adds a new device.</summary>
        public async Task AddAsync(Device device, CancellationToken ct)
        {
            try
            {
                _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
                {
                    VideoForensicsDbContext db = context.Db();
                    _ = db.Devices.Add(device);
                    _ = await db.SaveChangesAsync(ct);
                    return null;
                }, ct);
                _logger.LogDebug("Device added: {DeviceName}", device.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error adding device: {DeviceName}: {Message} ({ErrorType})", device.Name, ex.Message, ex.GetType().Name);
                throw;
            }
        }

        /// <summary>Updates an existing device.</summary>
        public async Task UpdateAsync(Device device, CancellationToken ct)
        {
            try
            {
                _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
                {
                    VideoForensicsDbContext db = context.Db();
                    _ = db.Devices.Update(device);
                    _ = await db.SaveChangesAsync(ct);
                    return null;
                }, ct);
                _logger.LogDebug("Device updated: {DeviceName}", device.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error updating device: {DeviceName}: {Message} ({ErrorType})", device.Name, ex.Message, ex.GetType().Name);
                throw;
            }
        }

        /// <summary>Updates the last successful pull timestamp for a device.</summary>
        public async Task UpdateLastSuccessfulPullAsync(Guid deviceId, DateTime pulledAtUtc, CancellationToken ct)
        {
            Device? device = null;
            try
            {
                bool updated = await _unitOfWork.ExecuteAsync(async context =>
                {
                    VideoForensicsDbContext db = context.Db();
                    device = await db.Devices.FirstOrDefaultAsync(d => d.Id == deviceId, ct);
                    if (device == null)
                    {
                        return false;
                    }

                    device.LastSuccessfulPullAtUtc = pulledAtUtc;
                    _ = await db.SaveChangesAsync(ct);
                    return true;
                }, ct);

                if (updated && device != null)
                {
                    _logger.LogDebug("Device watermark updated: {DeviceName} ({PulledAtUtc})", device.Name, pulledAtUtc);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error updating device watermark: {DeviceName}: {Message} ({ErrorType})", device?.Name ?? "unknown device", ex.Message, ex.GetType().Name);
                throw;
            }
        }

        /// <summary>Deletes a device.</summary>
        public async Task DeleteAsync(Guid deviceId, CancellationToken ct)
        {
            Device? device = null;
            try
            {
                bool deleted = await _unitOfWork.ExecuteAsync(async context =>
                {
                    VideoForensicsDbContext db = context.Db();
                    device = await db.Devices.FirstOrDefaultAsync(d => d.Id == deviceId, ct);
                    if (device == null)
                    {
                        return false;
                    }

                    _ = db.Devices.Remove(device);
                    _ = await db.SaveChangesAsync(ct);
                    return true;
                }, ct);

                if (deleted && device != null)
                {
                    _logger.LogDebug("Device deleted: {DeviceName}", device.Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Error deleting device: {DeviceName}: {Message} ({ErrorType})", device?.Name ?? "unknown device", ex.Message, ex.GetType().Name);
                throw;
            }
        }
    }
}
