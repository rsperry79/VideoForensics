using VideoForensics.Data.Common.Contracts;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Core.Api.Contracts.Repositories
{
    /// <summary>Maps the Notice entity to its wire DTO.</summary>
    public static class NoticeDtoMapping
    {
        /// <summary>Converts a Notice entity to a NoticeDto.</summary>
        public static NoticeDto ToDto(this Notice entity) =>
            new(entity.Id, entity.EventType, entity.Severity, entity.Details, entity.Audience, entity.TimestampUtc,
                entity.OperatorId, entity.ProviderAccountId, entity.ActionPath);
    }

    /// <summary>Server-side <see cref="INoticeRepository"/> over <see cref="INoticeWriteRepository"/>, mapping to DTOs.</summary>
    public sealed class NoticeDtoRepository(INoticeWriteRepository notices) : INoticeRepository
    {
        /// <inheritdoc />
        public async Task<IReadOnlyList<NoticeDto>> ListForOperatorAsync(Guid operatorId, bool isAdmin, bool includeDismissed, CancellationToken ct) =>
            (await notices.ListForOperatorAsync(operatorId, isAdmin, includeDismissed, ct)).Select(n => n.ToDto()).ToList();

        /// <inheritdoc />
        public Task<int> CountUndismissedForOperatorAsync(Guid operatorId, bool isAdmin, CancellationToken ct) =>
            notices.CountUndismissedForOperatorAsync(operatorId, isAdmin, ct);
    }

    /// <summary>Server-side <see cref="INoticeDismissalRepository"/> over <see cref="INoticeDismissalWriteRepository"/>.</summary>
    public sealed class NoticeDismissalDtoRepository(INoticeDismissalWriteRepository dismissals) : INoticeDismissalRepository
    {
        /// <inheritdoc />
        public Task DismissAsync(Guid noticeId, Guid operatorId, CancellationToken ct) =>
            dismissals.DismissAsync(noticeId, operatorId, ct);

        /// <inheritdoc />
        public Task DismissAllAsync(IEnumerable<Guid> noticeIds, Guid operatorId, CancellationToken ct) =>
            dismissals.DismissAllAsync(noticeIds, operatorId, ct);
    }
}
