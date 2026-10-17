namespace VideoForensics.Core.Providers.Common.Services
{
    /// <summary>
    /// Spaces provider media requests: each <see cref="WaitTurnAsync"/> returns only once <see cref="MinInterval"/> has passed
    /// since the previous turn started. Turns are serialized, so requests never overlap through this pacer. The clock and
    /// delay are injectable so tests run without sleeping.
    /// </summary>
    public sealed class MediaRequestPacer
    {
        private readonly Func<DateTime> _utcNow;
        private readonly Func<TimeSpan, CancellationToken, Task> _delay;
        private readonly SemaphoreSlim _turn = new(1, 1);
        private DateTime? _lastTurnUtc;

        /// <summary>Minimum time between the starts of consecutive turns.</summary>
        public TimeSpan MinInterval { get; set; }

        /// <summary>Creates a pacer. Defaults: the system clock, a real delay, and a one-second interval.</summary>
        public MediaRequestPacer(Func<DateTime>? utcNow = null, Func<TimeSpan, CancellationToken, Task>? delay = null, TimeSpan? minInterval = null)
        {
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
            _delay = delay ?? ((wait, ct) => Task.Delay(wait, ct));
            MinInterval = minInterval ?? TimeSpan.FromSeconds(1);
        }

        /// <summary>Waits until the minimum interval since the previous turn has passed, then starts this turn.</summary>
        public async Task WaitTurnAsync(CancellationToken cancellationToken)
        {
            await _turn.WaitAsync(cancellationToken);
            try
            {
                if (_lastTurnUtc.HasValue)
                {
                    TimeSpan remaining = _lastTurnUtc.Value + MinInterval - _utcNow();
                    if (remaining > TimeSpan.Zero)
                    {
                        await _delay(remaining, cancellationToken);
                    }
                }

                _lastTurnUtc = _utcNow();
            }
            finally
            {
                _ = _turn.Release();
            }
        }
    }
}
