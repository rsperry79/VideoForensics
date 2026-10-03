using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;

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
            // Build a Polly policy that retries on rate-limit errors with exponential backoff.
            // Hard bans (ThrottledException.IsHardBan = true) are not retried - they fail immediately.
            var retryPolicy = Policy
                .Handle<Exception>(ex => IsRateLimitError(ex) && (ex as Exceptions.ThrottledException)?.IsHardBan != true)
                .WaitAndRetryAsync(
                    retryCount: MaxRetries,
                    sleepDurationProvider: attempt =>
                    {
                        // Exponential backoff: 1s, 2s, 4s, 8s, ... capped at 30s
                        var delaySeconds = Math.Min(Math.Pow(2, attempt - 1), MaxDelayMs / 1000.0);
                        return TimeSpan.FromSeconds(delaySeconds);
                    },
                    onRetry: (outcome, timespan, retryCount, context) =>
                    {
                        _logger.LogWarning(
                            "Rate limit on {Operation} (attempt {Attempt}/{Max}). Waiting {DelayMs}ms before retry.",
                            operationName,
                            retryCount,
                            MaxRetries,
                            (int)timespan.TotalMilliseconds);
                    });

            // ExecuteAsync with Context and CancellationToken
            var context = new Polly.Context();
            await retryPolicy.ExecuteAsync(async (ctx, ct) => await operation(), context, cancellationToken);
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
