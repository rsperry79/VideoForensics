using VideoForensics.Core.Providers.Common.Contracts;

namespace VideoForensics.Core.Api.Contracts
{
    /// <summary>One operator-visible notice.</summary>
    public sealed record NoticeDto(
        Guid Id,
        string EventType,
        NoticeSeverity Severity,
        string? Details,
        int Audience,
        DateTime TimestampUtc,
        Guid? OperatorId,
        Guid? ProviderAccountId,
        string? ActionPath);

    /// <summary>Number of notices the signed-in operator has not dismissed.</summary>
    public sealed record UndismissedNoticeCountDto(int Count);
}
