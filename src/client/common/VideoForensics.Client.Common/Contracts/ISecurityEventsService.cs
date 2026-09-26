namespace VideoForensics.Client.Common.Contracts
{
    /// <summary>
    /// A single security event (login attempt, breach, lockout, etc.) as shown to a UI client -
    /// deliberately independent of <c>VideoForensics.Api.Contracts.SecurityEventDto</c> (the wire DTO):
    /// this project sits below <c>VideoForensics.Api.Contracts</c> in the dependency graph (the same
    /// reason <see cref="IMediaContentUrlProvider"/> returns a plain dictionary rather than an
    /// Api.Contracts type), so <see cref="ISecurityEventsService"/>'s Remote/Local implementations
    /// translate to/from the wire DTO at the HTTP boundary.
    /// </summary>
    public record SecurityEventSummary(
        Guid Id,
        Guid OperatorId,
        string EventType,
        bool Success,
        string? IpAddress,
        DateTime OccurredAtUtc,
        string? Reason
    );

    /// <summary>
    /// Service for querying operator security events (login attempts, breaches, lockouts, etc.).
    /// On a client host (MAUI), this is backed by HTTP calls to the server; the WebApp's own Blazor UI
    /// uses the same HTTP-backed implementation calling back into its own Minimal API (see
    /// <c>VideoForensics.WebApp.Services.SelfHttpServiceExtensions</c>), so the endpoint's authorization
    /// rule (self-service always allowed; cross-account requires SuperAdmin + Local tier) is the single,
    /// uniform source of truth for both hosts.
    /// Provides both self-service (caller's own events) and cross-account audit access (SuperAdmin only).
    /// </summary>
    public interface ISecurityEventsService
    {
        /// <summary>
        /// Retrieves security events for the caller (self-service) or a specific operator (cross-account audit).
        /// </summary>
        /// <param name="operatorId">Optional operator ID to query. If null, returns the caller's own events. Cross-account queries require SuperAdmin + Local tier.</param>
        /// <param name="offset">Number of events to skip (for pagination). Defaults to 0.</param>
        /// <param name="limit">Maximum number of events to return. Defaults to 20, max 1000.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>List of security events matching the query parameters.</returns>
        /// <exception cref="HttpRequestException">
        /// Thrown with <see cref="HttpRequestException.StatusCode"/> set to Unauthorized if the caller is
        /// not signed in, or Forbidden if a cross-account query is attempted by a caller who is not
        /// SuperAdmin on the Local network tier.
        /// </exception>
        Task<IReadOnlyList<SecurityEventSummary>> GetEventsAsync(Guid? operatorId, int offset, int limit, CancellationToken ct);
    }
}
