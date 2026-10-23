using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using System.Runtime.CompilerServices;

using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;
using VideoForensics.Data.Database.DbContext;

namespace VideoForensics.Data.Database.Repositories
{
    /// <summary>Repository implementation for security audit logging.</summary>
    public class SecurityAuditService : ISecurityAuditService
    {
        private readonly IDbContextFactory<VideoForensicsDbContext> _factory;
        private readonly ILogger<SecurityAuditService> _logger;

        public SecurityAuditService(IDbContextFactory<VideoForensicsDbContext> factory, ILogger<SecurityAuditService> logger)
        {
            _factory = factory;
            _logger = logger;
        }

        public async Task RecordLoginAttemptAsync(Guid operatorId, string? ipAddress, bool success, string? reason, CancellationToken ct)
        {
            SecurityEventType eventType = success ? SecurityEventType.LoginAttemptSuccess : SecurityEventType.LoginAttemptFailure;
            await RecordEventAsync(operatorId, eventType, ipAddress, success, reason, ct);
        }

        public async Task RecordBreachDetectionAsync(Guid operatorId, string? ipAddress, string reason, CancellationToken ct)
        {
            await RecordEventAsync(operatorId, SecurityEventType.BreachDetected, ipAddress, false, reason, ct);
        }

        public async Task RecordAccountLockoutAsync(Guid operatorId, string? ipAddress, CancellationToken ct)
        {
            await RecordEventAsync(operatorId, SecurityEventType.LockedOut, ipAddress, false, null, ct);
        }

        public async Task RecordAccountLockoutReleasedAsync(Guid operatorId, CancellationToken ct)
        {
            await RecordEventAsync(operatorId, SecurityEventType.LockedOutReleased, null, true, null, ct);
        }

        public async IAsyncEnumerable<SecurityEventDto> GetOperatorEventsAsync(Guid operatorId, int skip, int take, [EnumeratorCancellation] CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            IQueryable<SecurityEvent> events = db.SecurityEvents
                .Where(e => e.OperatorId == operatorId)
                .OrderByDescending(e => e.OccurredAtUtc)
                .Skip(skip)
                .Take(take);

            await foreach (SecurityEvent? @event in events.AsAsyncEnumerable().WithCancellation(ct))
            {
                yield return MapToDto(@event);
            }
        }

        public async IAsyncEnumerable<SecurityEventDto> GetAllEventsAsync(int skip, int take, [EnumeratorCancellation] CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            IQueryable<SecurityEvent> events = db.SecurityEvents
                .OrderByDescending(e => e.OccurredAtUtc)
                .Skip(skip)
                .Take(take);

            await foreach (SecurityEvent? @event in events.AsAsyncEnumerable().WithCancellation(ct))
            {
                yield return MapToDto(@event);
            }
        }

        private async Task RecordEventAsync(Guid operatorId, SecurityEventType eventType, string? ipAddress, bool success, string? reason, CancellationToken ct)
        {
            await using VideoForensicsDbContext db = await _factory.CreateDbContextAsync(ct);
            var securityEvent = new SecurityEvent
            {
                Id = Guid.NewGuid(),
                OperatorId = operatorId,
                EventType = eventType,
                Success = success,
                IpAddress = ipAddress,
                OccurredAtUtc = DateTime.UtcNow,
                Reason = reason,
                CreatedAtUtc = DateTime.UtcNow
            };

            _ = db.SecurityEvents.Add(securityEvent);
            _ = await db.SaveChangesAsync(ct);
            _logger.LogDebug("Security event recorded: {EventType} for operator {OperatorId}", eventType, operatorId);
        }

        private static SecurityEventDto MapToDto(SecurityEvent @event)
        {
            return new SecurityEventDto
            {
                Id = @event.Id,
                OperatorId = @event.OperatorId,
                EventType = @event.EventType.ToString(),
                Success = @event.Success,
                IpAddress = @event.IpAddress,
                OccurredAtUtc = @event.OccurredAtUtc,
                Reason = @event.Reason
            };
        }
    }
}
