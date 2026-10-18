using VideoForensics.Providers.Ring.Exceptions;

namespace VideoForensics.Providers.Ring.Services
{
    /// <summary>
    /// Ring-specific 429 handling for one media operation: waits out HTTP 429 throttling and retries the same operation.
    /// Waits the response's Retry-After, or <see cref="DefaultThrottleWait"/> when there is none. A hard ban is rethrown at
    /// once, because every retry would fail the same way until the ban expires.
    /// </summary>
    public static class ThrottleRetry
    {
        /// <summary>Wait used when a 429 carries no Retry-After.</summary>
        public static readonly TimeSpan DefaultThrottleWait = TimeSpan.FromSeconds(60);

        /// <summary>Runs <paramref name="operation"/>. Returns false when it is still throttled after <paramref name="maxAttempts"/> tries.</summary>
        public static async Task<bool> RunAsync(
            Func<Task> operation,
            Func<TimeSpan, CancellationToken, Task> wait,
            CancellationToken cancellationToken,
            int maxAttempts = 3)
        {
            for (int attempt = 1; ; attempt++)
            {
                try
                {
                    await operation();
                    return true;
                }
                catch (ThrottledException ex) when (!ex.IsHardBan)
                {
                    if (attempt >= maxAttempts)
                    {
                        return false;
                    }

                    await wait(ex.RetryAfter ?? DefaultThrottleWait, cancellationToken);
                }
            }
        }
    }
}
