namespace VideoForensics.Api.Contracts
{
    /// <summary>
    /// Lightweight operator summary used to populate cross-account pickers (e.g. the SuperAdmin
    /// security-events operator picker) without exposing the full Operator entity (password hash,
    /// security stamp, etc.) over the wire.
    /// </summary>
    /// <param name="Id">The operator's unique identifier.</param>
    /// <param name="DisplayName">The operator's display name, shown in pickers/lists.</param>
    /// <param name="Active">Whether the operator's account is currently active.</param>
    public record OperatorSummaryDto(
        Guid Id,
        string DisplayName,
        bool Active
    );
}
