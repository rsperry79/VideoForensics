using Microsoft.AspNetCore.SignalR.Client;

namespace VideoForensics.Hosting
{
    /// <summary>
    /// Automatic-reconnect policy for the live hub. Retries indefinitely with exponential backoff
    /// (base * 2^attempt) capped at a maximum delay. Returns null (stop) only for HTTP 401, because a rejected
    /// token cannot succeed on retry and the user must re-pair.
    /// </summary>
    /// <remarks>
    /// Why not the SignalR default: <c>WithAutomaticReconnect()</c> gives up after four attempts (about 40 s), which
    /// left the client dead after any server restart longer than that.
    /// </remarks>
    internal sealed class CappedBackoffRetryPolicy : IRetryPolicy
    {
        // 2^30 seconds is already far past any sane cap. Clamping the exponent keeps the math from overflowing
        // when a caller passes a very large attempt count.
        private const int MaxExponent = 30;

        private readonly TimeSpan _baseDelay;
        private readonly TimeSpan _maxDelay;

        public CappedBackoffRetryPolicy(TimeSpan baseDelay, TimeSpan maxDelay)
        {
            if (baseDelay <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(nameof(baseDelay), "Base delay must be positive.");
            }

            if (maxDelay < baseDelay)
            {
                throw new ArgumentOutOfRangeException(nameof(maxDelay), "Max delay must not be less than the base delay.");
            }

            _baseDelay = baseDelay;
            _maxDelay = maxDelay;
        }

        /// <inheritdoc />
        public TimeSpan? NextRetryDelay(RetryContext retryContext)
        {
            ArgumentNullException.ThrowIfNull(retryContext);

            return HubAuthFailure.IsAuthFailure(retryContext.RetryReason)
                ? null
                : DelayForAttempt(retryContext.PreviousRetryCount);
        }

        /// <summary>Delay before the attempt that follows <paramref name="previousRetryCount"/> failed attempts.</summary>
        public TimeSpan DelayForAttempt(long previousRetryCount)
        {
            int exponent = (int)Math.Min(Math.Max(previousRetryCount, 0), MaxExponent);
            double seconds = _baseDelay.TotalSeconds * Math.Pow(2, exponent);
            return TimeSpan.FromSeconds(Math.Min(seconds, _maxDelay.TotalSeconds));
        }
    }
}
