using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Repository implementation for PairedDevice entities.</summary>
    public class PairedDeviceRepository : IPairedDeviceRepository
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<PairedDeviceRepository> _logger;

        public PairedDeviceRepository(IUnitOfWork unitOfWork, ILogger<PairedDeviceRepository> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public Task<PairedDevice?> GetAsync(Guid pairedDeviceId, CancellationToken ct)
        {
            return _unitOfWork.ReadAsync(context => context.Db().PairedDevices.FirstOrDefaultAsync(d => d.Id == pairedDeviceId, ct), ct);
        }

        public Task<PairedDevice?> GetByWebAuthnCredentialIdAsync(string credentialId, CancellationToken ct)
        {
            return _unitOfWork.ReadAsync(context => context.Db().PairedDevices.FirstOrDefaultAsync(
                d => d.WebAuthnCredentialId == credentialId && d.RevokedAtUtc == null, ct), ct);
        }

        public Task<PairedDevice?> GetByFallbackApiKeyHashAsync(string apiKeyHash, CancellationToken ct)
        {
            return _unitOfWork.ReadAsync(context => context.Db().PairedDevices.FirstOrDefaultAsync(
                d => d.FallbackApiKeyHash == apiKeyHash && d.RevokedAtUtc == null, ct), ct);
        }

        public async Task<PairedDevice> AddAsync(PairedDevice device, CancellationToken ct)
        {
            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                VideoForensicsDbContext db = context.Db();
                _ = db.PairedDevices.Add(device);
                _ = await db.SaveChangesAsync(ct);
                return null;
            }, ct);
            _logger.LogDebug("Paired device created: {PairedDeviceName} for Operator {OperatorId}, role {Role}",
                device.DeviceName, device.OperatorId, device.Role);
            return device;
        }

        public async Task UpdateAsync(PairedDevice device, CancellationToken ct)
        {
            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                VideoForensicsDbContext db = context.Db();
                _ = db.PairedDevices.Update(device);
                _ = await db.SaveChangesAsync(ct);
                return null;
            }, ct);
        }

        public Task<IReadOnlyList<PairedDevice>> ListAsync(CancellationToken ct)
        {
            return _unitOfWork.ReadAsync<IReadOnlyList<PairedDevice>>(async context =>
            {
                return await context.Db().PairedDevices.OrderByDescending(d => d.PairedAtUtc).ToListAsync(ct);
            }, ct);
        }

        public Task<IReadOnlyList<PairedDevice>> ListForOperatorAsync(Guid operatorId, CancellationToken ct)
        {
            return _unitOfWork.ReadAsync<IReadOnlyList<PairedDevice>>(async context =>
            {
                return await context.Db().PairedDevices
                    .Where(d => d.OperatorId == operatorId)
                    .OrderByDescending(d => d.PairedAtUtc)
                    .ToListAsync(ct);
            }, ct);
        }

        public async Task RevokeAsync(Guid pairedDeviceId, string reason, CancellationToken ct)
        {
            PairedDevice? device = await _unitOfWork.ExecuteAsync(async context =>
            {
                VideoForensicsDbContext db = context.Db();
                PairedDevice? found = await db.PairedDevices.FirstOrDefaultAsync(d => d.Id == pairedDeviceId, ct);
                if (found == null)
                {
                    return found;
                }

                found.RevokedAtUtc = DateTime.UtcNow;
                found.RevokedReason = reason;
                _ = await db.SaveChangesAsync(ct);
                return found;
            }, ct);

            if (device == null)
            {
                return;
            }

            _logger.LogInformation("Paired device revoked: {PairedDeviceName} - {Reason}", device.DeviceName, SanitizeForLog(reason));
        }

        public async Task<bool> DeleteRevokedAsync(Guid pairedDeviceId, CancellationToken ct)
        {
            PairedDevice? device = await _unitOfWork.ExecuteAsync(async context =>
            {
                VideoForensicsDbContext db = context.Db();
                // The RevokedAtUtc filter is in the query, not checked after load, so an active device
                // can never match and is never deleted even if a caller skips the revoke step.
                PairedDevice? found = await db.PairedDevices
                    .FirstOrDefaultAsync(d => d.Id == pairedDeviceId && d.RevokedAtUtc != null, ct);
                if (found == null)
                {
                    return found;
                }

                _ = db.PairedDevices.Remove(found);
                _ = await db.SaveChangesAsync(ct);
                return found;
            }, ct);

            if (device == null)
            {
                return false;
            }

            _logger.LogInformation("Paired device removed: {PairedDeviceName}", device.DeviceName);
            return true;
        }

        public async Task<IReadOnlyList<Guid>> RevokeAllForOperatorAsync(Guid operatorId, string reason, CancellationToken ct)
        {
            (int count, IReadOnlyList<Guid> ids) = await _unitOfWork.ExecuteAsync(async context =>
            {
                VideoForensicsDbContext db = context.Db();
                List<PairedDevice> devices = await db.PairedDevices
                    .Where(d => d.OperatorId == operatorId && d.RevokedAtUtc == null)
                    .ToListAsync(ct);

                DateTime now = DateTime.UtcNow;
                foreach (PairedDevice? device in devices)
                {
                    device.RevokedAtUtc = now;
                    device.RevokedReason = reason;
                }

                _ = await db.SaveChangesAsync(ct);
                return (devices.Count, (IReadOnlyList<Guid>)devices.Select(d => d.Id).ToList());
            }, ct);

            _logger.LogInformation("Revoked all {Count} active device(s) for Operator {OperatorId} - {Reason}", count, operatorId, SanitizeForLog(reason));
            return ids;
        }

        public async Task RecordSuccessfulAuthAsync(Guid pairedDeviceId, uint newSignCount, string? sourceIp, NetworkTier tier, CancellationToken ct)
        {
            _ = await _unitOfWork.ExecuteAsync<object?>(async context =>
            {
                VideoForensicsDbContext db = context.Db();
                PairedDevice? device = await db.PairedDevices.FirstOrDefaultAsync(d => d.Id == pairedDeviceId, ct);
                if (device == null)
                {
                    return null;
                }

                device.WebAuthnSignCount = newSignCount;
                device.LastSeenAtUtc = DateTime.UtcNow;
                device.LastSeenIp = sourceIp;
                device.LastSeenTier = tier;
                _ = await db.SaveChangesAsync(ct);
                return null;
            }, ct);
        }

        /// <summary>Sanitizes a string for logging by replacing newline characters to prevent log forging.</summary>
        private static string SanitizeForLog(string value)
        {
            return value.Replace('\r', '_').Replace('\n', '_');
        }
    }
}
