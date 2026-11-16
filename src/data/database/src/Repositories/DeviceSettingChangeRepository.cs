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
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<DeviceSettingChangeRepository> _logger;

        /// <summary>Initializes a new instance of the DeviceSettingChangeRepository.</summary>
        public DeviceSettingChangeRepository(IUnitOfWork unitOfWork, ILogger<DeviceSettingChangeRepository> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        /// <summary>Appends one or more device setting change records.</summary>
        public async Task AddRangeAsync(IEnumerable<DeviceSettingChange> changes, CancellationToken ct)
        {
            try
            {
                _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
                {
                    VideoForensicsDbContext db = context.Db();
                    db.DeviceSettingChanges.AddRange(changes);
                    _ = await db.SaveChangesAsync(ct);
                    return null;
                }, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error appending device setting change records: {Message} ({ErrorType})", ex.Message, ex.GetType().Name);
                throw;
            }
        }

        /// <summary>Lists the change records for a device, newest first.</summary>
        public async Task<IReadOnlyList<DeviceSettingChange>> ListByDeviceAsync(Guid deviceId, CancellationToken ct)
        {
            return await _unitOfWork.ReadAsync(context => context.Db().DeviceSettingChanges
                .AsNoTracking()
                .Where(dsc => dsc.DeviceId == deviceId)
                .OrderByDescending(dsc => dsc.ChangedAtUtc)
                .ToListAsync(ct), ct);
        }
    }
}
