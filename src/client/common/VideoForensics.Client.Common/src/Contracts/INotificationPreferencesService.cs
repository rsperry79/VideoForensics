namespace VideoForensics.Client.Common.Contracts
{
    /// <summary>
    /// An operator's notification preferences as shown to a UI client - deliberately independent of
    /// <c>VideoForensics.Core.Api.Contracts.NotificationPreferencesDto</c> (the wire DTO): this project sits
    /// below <c>VideoForensics.Core.Api.Contracts</c> in the dependency graph, so
    /// <see cref="INotificationPreferencesService"/>'s Remote implementation translates to/from the wire DTO at the HTTP boundary.
    /// </summary>
    /// <param name="PushEnabled">Whether Web Push notifications are enabled for the operator.</param>
    /// <param name="MinimumSeverity">Lowest notice severity (0 Info to 3 Critical) that triggers a push.</param>
    /// <param name="EmailEnabled">Whether email notifications are enabled for the operator.</param>
    /// <param name="EmailEventTypes">Exact security audit event type names that trigger an email.</param>
    public sealed record NotificationPreferencesSummary(
        bool PushEnabled,
        int MinimumSeverity,
        bool EmailEnabled,
        IReadOnlyList<string> EmailEventTypes
    );

    /// <summary>
    /// Service for reading and replacing the signed-in operator's notification preferences.
    /// On a client host (MAUI), this is backed by HTTP calls to the server's <c>/api/v1/notifications/preferences</c> routes.
    /// </summary>
    public interface INotificationPreferencesService
    {
        /// <summary>
        /// Retrieves the caller's notification preferences, or the server defaults when none are stored.
        /// </summary>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The caller's current notification preferences.</returns>
        /// <exception cref="HttpRequestException">Thrown with <see cref="HttpRequestException.StatusCode"/> set to Unauthorized if the caller is not signed in.</exception>
        Task<NotificationPreferencesSummary> GetAsync(CancellationToken ct);

        /// <summary>
        /// Replaces the caller's notification preferences (upsert) and returns the stored result.
        /// </summary>
        /// <param name="preferences">The preferences to store for the caller.</param>
        /// <param name="ct">Cancellation token.</param>
        /// <returns>The preferences as stored by the server.</returns>
        /// <exception cref="HttpRequestException">Thrown on a non-success response, including BadRequest for an unknown severity or event type.</exception>
        Task<NotificationPreferencesSummary> SaveAsync(NotificationPreferencesSummary preferences, CancellationToken ct);
    }
}
