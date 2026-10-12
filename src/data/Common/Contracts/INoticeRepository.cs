using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Data.Common.Contracts
{
    /// <summary>Repository for persisted notification events (notices), enabling audit trail and historical view.</summary>
    public interface INoticeRepository
    {
        /// <summary>Persists a new notice event to the database.</summary>
        Task AddAsync(Notice notice, CancellationToken ct);

        /// <summary>Lists notices for an operator, excluding those dismissed by that operator (unless includeDismissed is true). Includes notices with Audience=All, plus Audience=AdminsOnly when isAdmin is true.</summary>
        Task<IReadOnlyList<Notice>> ListForOperatorAsync(Guid operatorId, bool isAdmin, bool includeDismissed = false, CancellationToken ct = default);

        /// <summary>Counts undismissed notices for an operator, for UI badge/indicator purposes. Includes Audience=AdminsOnly notices only when isAdmin is true.</summary>
        Task<int> CountUndismissedForOperatorAsync(Guid operatorId, bool isAdmin, CancellationToken ct);
    }
}
