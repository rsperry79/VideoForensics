using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Hosting
{
    /// <summary>Thin convenience wrapper over ISecurityAuditLogRepository so call sites don't hand-build a SecurityAuditLogEntry every time (plan §5.5).</summary>
    public interface ISecurityAuditLogger
    {
        Task LogAsync(string eventType, Guid? operatorId, Guid? pairedDeviceId, string? sourceIp, string? details, bool isUrgent, CancellationToken ct);
    }

    public class SecurityAuditLogger : ISecurityAuditLogger
    {
        private readonly ISecurityAuditLogRepository _repository;

        public SecurityAuditLogger(ISecurityAuditLogRepository repository)
        {
            _repository = repository;
        }

        public Task LogAsync(string eventType, Guid? operatorId, Guid? pairedDeviceId, string? sourceIp, string? details, bool isUrgent, CancellationToken ct)
        {
            return _repository.AppendAsync(new SecurityAuditLogEntry
            {
                Id = Guid.NewGuid(),
                TimestampUtc = DateTime.UtcNow,
                EventType = eventType,
                OperatorId = operatorId,
                PairedDeviceId = pairedDeviceId,
                SourceIp = sourceIp,
                Details = details,
                IsUrgent = isUrgent
            }, ct);
        }
    }
}
