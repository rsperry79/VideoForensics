using Microsoft.Extensions.Logging;

namespace VideoForensics.Providers.Ring.Services
{
    /// <summary>
    /// Encapsulates retry logic with exponential backoff for Ring API operations.
    /// Detects rate limit errors and applies appropriate delays between retries.
    /// </summary>
    public interface IRingRetryPolicy
    {
        /// <summary>
        /// Retries an async operation with exponential backoff on rate limit errors.
        /// Propagates non-rate-limit exceptions immediately. Rate limit errors trigger
        /// configurable delays between retries (up to MaxDelayMs) until MaxRetries is reached.
        /// </summary>
        Task RetryWithBackoffAsync(Func<Task> operation, string operationName, CancellationToken cancellationToken);

        /// <summary>
        /// Determines if an exception represents a rate limit error from the Ring API.
        /// Checks exception message for rate limit indicators.
        /// </summary>
        bool IsRateLimitError(Exception ex);
    }

    public class RingRetryPolicy : IRingRetryPolicy
    {
        private readonly ILogger _logger;

        private const int MaxRetries = 3;
        private const int InitialDelayMs = 1000;
        private const int MaxDelayMs = 30000;

        public RingRetryPolicy(ILogger logger)
        {
            _logger = logger;
        }

        public async Task RetryWithBackoffAsync(Func<Task> operation, string operationName, CancellationToken cancellationToken)
        {
            int delayMs = InitialDelayMs;

            for (int attempt = 1; attempt <= MaxRetries; attempt++)
            {
                try
                {
                    await operation();
                    return;
                }
                // A hard ban (see Session.GetRateLimitBanUntilUtc) fails every attempt identically
                // with no network call - retrying here just re-runs this backoff loop for zero chance
                // of success, so let it propagate immediately instead of grinding through it.
                catch (Exception ex) when (IsRateLimitError(ex) && attempt < MaxRetries && (ex as VideoForensics.Providers.Ring.Exceptions.ThrottledException)?.IsHardBan != true)
                {
                    _logger.LogWarning("Rate limit on {Operation} (attempt {Attempt}/{Max}). Waiting {DelayMs}ms before retry.",
                        operationName, attempt, MaxRetries, delayMs);

                    await Task.Delay(delayMs, cancellationToken);
                    delayMs = Math.Min(delayMs * 2, MaxDelayMs);
                }
            }

            // Final attempt without catch
            await operation();
        }

        public bool IsRateLimitError(Exception ex)
        {
            string message = ex.Message ?? string.Empty;

            return message.Contains("too many requests", StringComparison.OrdinalIgnoreCase) ||
                   message.Contains("rate limit", StringComparison.OrdinalIgnoreCase) ||
                   message.Contains("429", StringComparison.OrdinalIgnoreCase) ||
                   message.Contains("denied by Ring", StringComparison.OrdinalIgnoreCase);
        }
    }
}
