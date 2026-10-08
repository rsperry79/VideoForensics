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

    /// <summary>
    /// Wire DTO for an operator's UI mode and lock status.
    /// </summary>
    /// <param name="Mode">The UI mode: "Standard" or "Simple".</param>
    /// <param name="Locked">When true, the UI mode is locked and the operator cannot change it.</param>
    public record OperatorUiModeDto(
        string Mode,
        bool Locked
    );

    /// <summary>
    /// Request body to set an operator's UI mode and lock status.
    /// </summary>
    /// <param name="Mode">The UI mode: "Standard" or "Simple".</param>
    /// <param name="Locked">When true, lock the UI mode so the operator cannot change it.</param>
    public record SetOperatorUiModeRequest(
        string Mode,
        bool Locked
    );
}