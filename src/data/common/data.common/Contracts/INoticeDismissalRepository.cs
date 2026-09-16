using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Repository for per-operator notice dismissals (suppressing notices for a single operator).</summary>
    public interface INoticeDismissalRepository
    {
        /// <summary>Records an operator's dismissal of a notice (idempotent - multiple calls with the same (noticeId, operatorId) are safe).</summary>
        Task DismissAsync(Guid noticeId, Guid operatorId, CancellationToken ct);

        /// <summary>Dismisses multiple notices for an operator in a single operation (idempotent).</summary>
        Task DismissAllAsync(IEnumerable<Guid> noticeIds, Guid operatorId, CancellationToken ct);
    }
}
