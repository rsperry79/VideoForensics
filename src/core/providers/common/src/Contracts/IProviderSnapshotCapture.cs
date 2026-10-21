namespace VideoForensics.Core.Providers.Common.Contracts
{
    /// <summary>
    /// Runs one snapshot capture attempt for one device: trigger a fresh frame, wait, then read the latest frame.
    /// Implemented by providers that can capture snapshots; the server-side snapshot capture service calls it.
    /// </summary>
    public interface IProviderSnapshotCapture
    {
        /// <summary>
        /// Runs one attempt. Provider HTTP failures and rate limits come back as a <see cref="SnapshotCaptureAttempt"/>,
        /// not as exceptions. Throws only when the account has no usable session, or on cancellation.
        /// </summary>
        /// <param name="providerAccountId">Internal account GUID; used to pick the account's session and request pacer.</param>
        /// <param name="providerDeviceId">The provider's device id (for Ring, the numeric doorbot id).</param>
        /// <param name="settleDelay">How long to wait between the trigger and the read.</param>
        Task<SnapshotCaptureAttempt> CaptureAsync(Guid providerAccountId, string providerDeviceId, TimeSpan settleDelay, CancellationToken cancellationToken);
    }

    /// <summary>Outcome of one <see cref="IProviderSnapshotCapture.CaptureAsync"/> attempt.</summary>
    /// <param name="HttpStatus">
    /// The status the attempt ended with, or null when no HTTP status was observed (for example a network failure).
    /// A null status means nothing can be recorded for the attempt.
    /// </param>
    /// <param name="Image">The JPEG bytes when the attempt succeeded (status 200); null otherwise.</param>
    /// <param name="ThrottlePause">Set on a rate limit or hard ban: how long the account must not be called again.</param>
    public sealed record SnapshotCaptureAttempt(int? HttpStatus, byte[]? Image, TimeSpan? ThrottlePause);
}
