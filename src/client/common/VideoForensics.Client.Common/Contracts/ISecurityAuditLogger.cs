namespace VideoForensics.Client.Common.Contracts
{
    /// <summary>Thin convenience wrapper over ISecurityAuditLogRepository so call sites don't hand-build a SecurityAuditLogEntry every time (plan §5.5).</summary>
    public interface ISecurityAuditLogger
    {
        Task LogAsync(string eventType, Guid? operatorId, Guid? pairedDeviceId, string? sourceIp, string? details, bool isUrgent, CancellationToken ct);
    }
}
