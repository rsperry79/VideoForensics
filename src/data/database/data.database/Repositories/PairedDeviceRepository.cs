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
        private readonly IDbContextFactory<VideoForensicsDbContext> _factory;
        private readonly ILogger<PairedDeviceRepository> _logger;

        public PairedDeviceRepository(IDbContextFactory<VideoForensicsDbContext> factory, ILogger<PairedDeviceRepository> logger)
        {
            _factory = factory;
            _logger = logger;
        }

        public async Task<PairedDevice?> GetAsync(Guid pairedDeviceId, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            return await db.PairedDevices.FirstOrDefaultAsync(d => d.Id == pairedDeviceId, ct);
        }

        public async Task<PairedDevice?> GetByWebAuthnCredentialIdAsync(string credentialId, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            return await db.PairedDevices.FirstOrDefaultAsync(
                d => d.WebAuthnCredentialId == credentialId && d.RevokedAtUtc == null, ct);
        }

        public async Task<PairedDevice?> GetByFallbackApiKeyHashAsync(string apiKeyHash, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            return await db.PairedDevices.FirstOrDefaultAsync(
                d => d.FallbackApiKeyHash == apiKeyHash && d.RevokedAtUtc == null, ct);
        }

        public async Task<PairedDevice> AddAsync(PairedDevice device, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            db.PairedDevices.Add(device);
            await db.SaveChangesAsync(ct);
            _logger.LogInformation("Paired device created: {PairedDeviceId} for Operator {OperatorId}, role {Role}",
                device.Id, device.OperatorId, device.Role);
            return device;
        }

        public async Task UpdateAsync(PairedDevice device, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            db.PairedDevices.Update(device);
            await db.SaveChangesAsync(ct);
        }

        public async Task<IReadOnlyList<PairedDevice>> ListAsync(CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            return await db.PairedDevices.OrderByDescending(d => d.PairedAtUtc).ToListAsync(ct);
        }

        public async Task<IReadOnlyList<PairedDevice>> ListForOperatorAsync(Guid operatorId, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            return await db.PairedDevices
                .Where(d => d.OperatorId == operatorId)
                .OrderByDescending(d => d.PairedAtUtc)
                .ToListAsync(ct);
        }

        public async Task RevokeAsync(Guid pairedDeviceId, string reason, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            var device = await db.PairedDevices.FirstOrDefaultAsync(d => d.Id == pairedDeviceId, ct);
            if (device == null)
            {
                return;
            }

            device.RevokedAtUtc = DateTime.UtcNow;
            device.RevokedReason = reason;
            await db.SaveChangesAsync(ct);
            _logger.LogWarning("Paired device revoked: {PairedDeviceId} - {Reason}", pairedDeviceId, reason);
        }

        public async Task<IReadOnlyList<Guid>> RevokeAllForOperatorAsync(Guid operatorId, string reason, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            var devices = await db.PairedDevices
                .Where(d => d.OperatorId == operatorId && d.RevokedAtUtc == null)
                .ToListAsync(ct);

            var now = DateTime.UtcNow;
            foreach (var device in devices)
            {
                device.RevokedAtUtc = now;
                device.RevokedReason = reason;
            }

            await db.SaveChangesAsync(ct);
            _logger.LogWarning("Revoked all {Count} active device(s) for Operator {OperatorId} - {Reason}", devices.Count, operatorId, reason);
            return devices.Select(d => d.Id).ToList();
        }

        public async Task RecordSuccessfulAuthAsync(Guid pairedDeviceId, uint newSignCount, string? sourceIp, NetworkTier tier, CancellationToken ct)
        {
            await using var db = await _factory.CreateDbContextAsync(ct);
            var device = await db.PairedDevices.FirstOrDefaultAsync(d => d.Id == pairedDeviceId, ct);
            if (device == null)
            {
                return;
            }

            device.WebAuthnSignCount = newSignCount;
            device.LastSeenAtUtc = DateTime.UtcNow;
            device.LastSeenIp = sourceIp;
            device.LastSeenTier = tier;
            await db.SaveChangesAsync(ct);
        }
    }
}
