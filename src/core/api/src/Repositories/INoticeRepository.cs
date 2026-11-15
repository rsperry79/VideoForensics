namespace VideoForensics.Core.Api.Contracts.Repositories
{
    /// <summary>Operator notices, with DTO signatures. Client hosts use this interface. The server scopes results to the signed-in operator.</summary>
    public interface INoticeRepository
    {
        /// <summary>Lists the notices visible to an operator, optionally including dismissed ones.</summary>
        Task<IReadOnlyList<NoticeDto>> ListForOperatorAsync(Guid operatorId, bool isAdmin, bool includeDismissed, CancellationToken ct);

        /// <summary>Counts the notices visible to an operator that they have not dismissed.</summary>
        Task<int> CountUndismissedForOperatorAsync(Guid operatorId, bool isAdmin, CancellationToken ct);
    }
}
