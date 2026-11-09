using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Repository implementation for SnapshotCapture records.</summary>
    public class SnapshotCaptureRepository : ISnapshotCaptureRepository
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<SnapshotCaptureRepository> _logger;

        public SnapshotCaptureRepository(IUnitOfWork unitOfWork, ILogger<SnapshotCaptureRepository> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        /// <summary>Records one snapshot attempt (current or not).</summary>
        public async Task<SnapshotCapture> AddAsync(SnapshotCapture capture, CancellationToken ct)
        {
            return await _unitOfWork.ExecuteAsync<SnapshotCapture>(async context =>
            {
                VideoForensicsDbContext db = context.Db();
                try
                {
                    _ = db.SnapshotCaptures.Add(capture);
                    _ = await db.SaveChangesAsync(ct);
                    _logger.LogDebug(
                        "Snapshot capture recorded: {CaptureId} (device {DeviceId}, trigger: {Trigger}, status: {HttpStatus}, current: {IsCurrent})",
                        capture.Id, capture.DeviceId, capture.Trigger, capture.HttpStatus, capture.IsCurrent);
                    return capture;
                }
                catch (Exception ex)
                {
                    _logger.LogError("Error recording snapshot capture for device {DeviceId}: {Message} ({ErrorType})", capture.DeviceId, ex.Message, ex.GetType().Name);
                    throw;
                }
            }, ct);
        }

        /// <summary>Gets one capture attempt by its id, or null when no such capture exists.</summary>
        public async Task<SnapshotCapture?> GetAsync(Guid id, CancellationToken ct)
        {
            return await _unitOfWork.ReadAsync<SnapshotCapture?>(async context =>
            {
                return await context.Db().SnapshotCaptures
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == id, ct);
            }, ct);
        }

        /// <summary>Gets a device's current captures (IsCurrent == true) within a date range, oldest first.</summary>
        public async Task<IReadOnlyList<SnapshotCapture>> GetCurrentForDeviceAsync(Guid deviceId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
        {
            return await _unitOfWork.ReadAsync<IReadOnlyList<SnapshotCapture>>(async context =>
            {
                VideoForensicsDbContext db = context.Db();

                // Normalize to UTC so the comparison matches the UTC values stored by SnapshotCapture.Create.
                DateTimeOffset fromUtc = from.ToUniversalTime();
                DateTimeOffset toUtc = to.ToUniversalTime();

                return await db.SnapshotCaptures
                    .Where(c => c.DeviceId == deviceId && c.IsCurrent && c.CapturedAtUtc >= fromUtc && c.CapturedAtUtc <= toUtc)
                    .OrderBy(c => c.CapturedAtUtc)
                    .ToListAsync(ct);
            }, ct);
        }

        /// <summary>Gets every capture attempt (current or not) for a device within a date range, oldest first.</summary>
        public async Task<IReadOnlyList<SnapshotCapture>> GetAttemptsForDeviceAsync(Guid deviceId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
        {
            return await _unitOfWork.ReadAsync<IReadOnlyList<SnapshotCapture>>(async context =>
            {
                VideoForensicsDbContext db = context.Db();

                // Same UTC normalization as GetCurrentForDeviceAsync: stored values are UTC.
                DateTimeOffset fromUtc = from.ToUniversalTime();
                DateTimeOffset toUtc = to.ToUniversalTime();

                return await db.SnapshotCaptures
                    .Where(c => c.DeviceId == deviceId && c.CapturedAtUtc >= fromUtc && c.CapturedAtUtc <= toUtc)
                    .OrderBy(c => c.CapturedAtUtc)
                    .ToListAsync(ct);
            }, ct);
        }

        /// <summary>
        /// Gets which of the given devices have a capture attempt within a date range. Filtering and de-duplication run in SQL,
        /// so only the matching device ids come back.
        /// </summary>
        public async Task<IReadOnlySet<Guid>> GetDeviceIdsWithAttemptsAsync(IReadOnlyCollection<Guid> deviceIds, DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
        {
            if (deviceIds.Count == 0)
            {
                return new HashSet<Guid>();
            }

            return await _unitOfWork.ReadAsync<IReadOnlySet<Guid>>(async context =>
            {
                VideoForensicsDbContext db = context.Db();

                DateTimeOffset fromUtc = from.ToUniversalTime();
                DateTimeOffset toUtc = to.ToUniversalTime();

                List<Guid> ids = await db.SnapshotCaptures
                    .Where(c => deviceIds.Contains(c.DeviceId) && c.CapturedAtUtc >= fromUtc && c.CapturedAtUtc <= toUtc)
                    .Select(c => c.DeviceId)
                    .Distinct()
                    .ToListAsync(ct);

                return ids.ToHashSet();
            }, ct);
        }
    }
}
