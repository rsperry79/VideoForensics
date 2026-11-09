using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Repository implementation for append-only DeviceConfigSnapshot entities.</summary>
    public class DeviceConfigRepository : IDeviceConfigRepository
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<DeviceConfigRepository> _logger;

        /// <summary>Initializes a new instance of the DeviceConfigRepository.</summary>
        public DeviceConfigRepository(IUnitOfWork unitOfWork, ILogger<DeviceConfigRepository> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        /// <summary>Gets a device config snapshot by ID.</summary>
        public Task<DeviceConfigSnapshot?> GetAsync(Guid snapshotId, CancellationToken ct)
        {
            return _unitOfWork.ReadAsync(context => context.Db().DeviceConfigSnapshots.Include(dcs => dcs.CustomSettings).FirstOrDefaultAsync(dcs => dcs.Id == snapshotId, ct), ct);
        }

        /// <summary>Appends a new device configuration snapshot.</summary>
        public async Task<DeviceConfigSnapshot> AppendSnapshotAsync(DeviceConfigSnapshot snapshot, CancellationToken ct)
        {
            try
            {
                return await _unitOfWork.ExecuteAsync<DeviceConfigSnapshot>(async context =>
                {
                    VideoForensicsDbContext db = context.Db();
                    foreach (DeviceConfigSnapshotSetting setting in snapshot.CustomSettings)
                    {
                        setting.Id = setting.Id == Guid.Empty ? Guid.NewGuid() : setting.Id;
                        setting.SnapshotId = snapshot.Id;
                    }

                    _ = db.DeviceConfigSnapshots.Add(snapshot);
                    _ = await db.SaveChangesAsync(ct);
                    _logger.LogDebug("Device config snapshot appended: {SnapshotId} (device: {DeviceId})",
                        snapshot.Id, snapshot.DeviceId);
                    return snapshot;
                }, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError("Error appending device config snapshot for device {DeviceId}: {Message} ({ErrorType})", snapshot.DeviceId, ex.Message, ex.GetType().Name);
                throw;
            }
        }

        /// <summary>Gets the latest device configuration snapshot for a device.</summary>
        public Task<DeviceConfigSnapshot?> GetLatestAsync(Guid deviceId, CancellationToken ct)
        {
            return _unitOfWork.ReadAsync(context => context.Db().DeviceConfigSnapshots
                .Include(dcs => dcs.CustomSettings)
                .Where(dcs => dcs.DeviceId == deviceId)
                .OrderByDescending(dcs => dcs.CapturedAtUtc)
                .FirstOrDefaultAsync(ct), ct);
        }

        /// <summary>Gets the full history of device configuration snapshots for a device.</summary>
        public async Task<IReadOnlyList<DeviceConfigSnapshot>> GetHistoryAsync(Guid deviceId, CancellationToken ct)
        {
            return await _unitOfWork.ReadAsync(context => context.Db().DeviceConfigSnapshots
                .Include(dcs => dcs.CustomSettings)
                .Where(dcs => dcs.DeviceId == deviceId)
                .OrderByDescending(dcs => dcs.CapturedAtUtc)
                .ToListAsync(ct), ct);
        }

        /// <summary>Lists all device config snapshots.</summary>
        public async Task<IReadOnlyList<DeviceConfigSnapshot>> ListAsync(CancellationToken ct)
        {
            return await _unitOfWork.ReadAsync(context => context.Db().DeviceConfigSnapshots.Include(dcs => dcs.CustomSettings).ToListAsync(ct), ct);
        }
    }
}
