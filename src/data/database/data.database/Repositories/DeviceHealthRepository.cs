using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Repository implementation for DeviceHealth time-series entities.</summary>
    public class DeviceHealthRepository : IDeviceHealthRepository
    {
        private readonly IDbContextFactory<VideoForensicsDbContext> _factory;
        private readonly ILogger<DeviceHealthRepository> _logger;

        public DeviceHealthRepository(IDbContextFactory<VideoForensicsDbContext> factory, ILogger<DeviceHealthRepository> logger)
        {
            _factory = factory;
            _logger = logger;
        }

        /// <summary>Records a new device health metric.</summary>
        public async Task<DeviceHealth> AddAsync(DeviceHealth health, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            try
            {
                _ = db.DeviceHealths.Add(health);
                _ = await db.SaveChangesAsync(ct);
                _logger.LogInformation("Device health metric recorded: {HealthId} (device: {DeviceId}, online: {IsOnline})",
                    health.Id, health.DeviceId, health.IsOnline);
                return health;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording device health metric for device {DeviceId}", health.DeviceId);
                throw;
            }
        }

        /// <summary>Gets the latest health metric for a device.</summary>
        public async Task<DeviceHealth?> GetLatestAsync(Guid deviceId, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await db.DeviceHealths
                .Where(dh => dh.DeviceId == deviceId)
                .OrderByDescending(dh => dh.CapturedAtUtc)
                .FirstOrDefaultAsync(ct);
        }

        /// <summary>Gets the full health history for a device, newest first.</summary>
        public async Task<IReadOnlyList<DeviceHealth>> GetHistoryAsync(Guid deviceId, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await db.DeviceHealths
                .Where(dh => dh.DeviceId == deviceId)
                .OrderByDescending(dh => dh.CapturedAtUtc)
                .ToListAsync(ct);
        }
    }
}
