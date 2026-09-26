namespace VideoForensics.Client.Common.Contracts
{
    /// <summary>
    /// Lightweight operator summary for a UI client (e.g. a cross-account picker) - deliberately
    /// independent of <c>VideoForensics.Api.Contracts.OperatorSummaryDto</c> (the wire DTO); see
    /// <see cref="ISecurityEventsService"/>'s <c>SecurityEventSummary</c> doc comment for why.
    /// </summary>
    public record OperatorSummary(Guid Id, string DisplayName, bool Active);

    /// <summary>
    /// Service for admin-level operator management operations (approve, deactivate, unlock, etc).
    /// On a client host (MAUI), this is backed by HTTP calls to the server's device management endpoints;
    /// the WebApp's own Blazor UI uses the same HTTP-backed implementation calling back into its own
    /// Minimal API (see <c>VideoForensics.WebApp.Services.SelfHttpServiceExtensions</c>).
    /// </summary>
    public interface IAdminOperatorService
    {
        /// <summary>
        /// Manually unlocks a locked-out operator account by clearing failed login attempts and lockout time.
        /// This is a SuperAdmin-initiated operation.
        /// </summary>
        Task UnlockAsync(Guid operatorId, CancellationToken ct);

        /// <summary>
        /// Lists a lightweight summary (Id, DisplayName, Active) of every operator - used to populate
        /// cross-account pickers (e.g. the SuperAdmin security-events operator picker) without exposing
        /// the full Operator entity over the wire. Requires SuperAdmin + Local tier, matching the
        /// underlying device-management endpoint.
        /// </summary>
        Task<IReadOnlyList<OperatorSummary>> ListOperatorsAsync(CancellationToken ct);
    }
}
