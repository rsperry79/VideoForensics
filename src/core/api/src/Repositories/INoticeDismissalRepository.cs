namespace VideoForensics.Core.Api.Contracts.Repositories
{
    /// <summary>Per-operator notice dismissals, with DTO signatures. Client hosts use this interface.</summary>
    public interface INoticeDismissalRepository
    {
        /// <summary>Dismisses one notice for the signed-in operator.</summary>
        Task DismissAsync(Guid noticeId, Guid operatorId, CancellationToken ct);

        /// <summary>Dismisses several notices for the signed-in operator.</summary>
        Task DismissAllAsync(IEnumerable<Guid> noticeIds, Guid operatorId, CancellationToken ct);
    }
}
