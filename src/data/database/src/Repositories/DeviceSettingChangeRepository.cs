using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Repository implementation for the append-only DeviceSettingChange history.</summary>
    public class DeviceSettingChangeRepository : IDeviceSettingChangeRepository
    {
        private readonly IDbContextFactory<VideoForensicsDbContext> _factory;
        private readonly ILogger<DeviceSettingChangeRepository> _logger;

        /// <summary>Initializes a new instance of the DeviceSettingChangeRepository.</summary>
        public DeviceSettingChangeRepository(IDbContextFactory<VideoForensicsDbContext> factory, ILogger<DeviceSettingChangeRepository> logger)
        {
            _factory = factory;
            _logger = logger;
        }

        /// <summary>Appends one or more device setting change records.</summary>
        public async Task AddRangeAsync(IEnumerable<DeviceSettingChange> changes, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            try
            {
                db.DeviceSettingChanges.AddRange(changes);
                _ = await db.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error appending device setting change records");
                throw;
            }
        }

        /// <summary>Lists the change records for a device, newest first.</summary>
        public async Task<IReadOnlyList<DeviceSettingChange>> ListByDeviceAsync(Guid deviceId, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            return await db.DeviceSettingChanges
                .AsNoTracking()
                .Where(dsc => dsc.DeviceId == deviceId)
                .OrderByDescending(dsc => dsc.ChangedAtUtc)
                .ToListAsync(ct);
        }
    }
}
