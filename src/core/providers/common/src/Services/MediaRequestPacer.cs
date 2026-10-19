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

        // Guards the state below, which RecordRateLimited can change while a turn is waiting.
        private readonly object _state = new();
        private TimeSpan _minInterval;
        private DateTime? _pauseUntilUtc;

        /// <summary>Default pause after a rate limit that carries no Retry-After.</summary>
        public static readonly TimeSpan DefaultRateLimitPause = TimeSpan.FromSeconds(60);

        /// <summary>Minimum time between the starts of consecutive turns.</summary>
        public TimeSpan MinInterval
        {
            get { lock (_state) { return _minInterval; } }
            set { lock (_state) { _minInterval = value; } }
        }

        /// <summary>Total extra spacing added by <see cref="RecordRateLimited"/>. Survives <see cref="MinInterval"/> being reset.</summary>
        public TimeSpan RateLimitPenalty { get; private set; }

        /// <summary>Creates a pacer. Defaults: the system clock, a real delay, and a one-second interval.</summary>
        public MediaRequestPacer(Func<DateTime>? utcNow = null, Func<TimeSpan, CancellationToken, Task>? delay = null, TimeSpan? minInterval = null)
        {
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
            _delay = delay ?? ((wait, ct) => Task.Delay(wait, ct));
            _minInterval = minInterval ?? TimeSpan.FromSeconds(1);
        }

        /// <summary>
        /// Records a rate limit from the provider: no turn starts before the pause (Retry-After, or 60 seconds when absent)
        /// has passed, and <see cref="MinInterval"/> grows by one second. Each call adds another second; nothing speeds it back up.
        /// </summary>
        public void RecordRateLimited(TimeSpan? retryAfter)
        {
            lock (_state)
            {
                DateTime pauseUntil = _utcNow() + (retryAfter ?? DefaultRateLimitPause);
                // A later, shorter Retry-After must not shorten a pause already in force.
                if (!_pauseUntilUtc.HasValue || pauseUntil > _pauseUntilUtc.Value)
                {
                    _pauseUntilUtc = pauseUntil;
                }

                _minInterval += TimeSpan.FromSeconds(1);
                RateLimitPenalty += TimeSpan.FromSeconds(1);
            }
        }

        /// <summary>Waits until the minimum interval since the previous turn, and any rate-limit pause, has passed, then starts this turn.</summary>
        public async Task WaitTurnAsync(CancellationToken cancellationToken)
        {
            await _turn.WaitAsync(cancellationToken);
            try
            {
                DateTime? notBefore;
                lock (_state)
                {
                    notBefore = _lastTurnUtc.HasValue ? _lastTurnUtc.Value + _minInterval : null;
                    if (_pauseUntilUtc.HasValue && (!notBefore.HasValue || _pauseUntilUtc.Value > notBefore.Value))
                    {
                        notBefore = _pauseUntilUtc.Value;
                    }
                }

                if (notBefore.HasValue)
                {
                    TimeSpan remaining = notBefore.Value - _utcNow();
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
