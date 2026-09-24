namespace VideoForensics.Hosting.Contracts
{
    /// <summary>
    /// Service for admin-level operator management operations (approve, deactivate, unlock, etc).
    /// On a client host (MAUI), this is backed by HTTP calls to the server's device management endpoints.
    /// </summary>
    public interface IAdminOperatorService
    {
        /// <summary>
        /// Manually unlocks a locked-out operator account by clearing failed login attempts and lockout time.
        /// This is a SuperAdmin-initiated operation.
        /// </summary>
        Task UnlockAsync(Guid operatorId, CancellationToken ct);
    }
}
