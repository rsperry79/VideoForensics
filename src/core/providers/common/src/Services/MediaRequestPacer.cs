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
        private bool _inRateLimitIncident;
        private bool _hardBanSinceSuccess;
        private DateTime? _hardBanWindowUntilUtc;

        /// <summary>Default pause after a rate limit that carries no Retry-After.</summary>
        public static readonly TimeSpan DefaultRateLimitPause = TimeSpan.FromSeconds(60);

        /// <summary>Pause for a hard ban that is not preceded by another hard ban since the last successful request.</summary>
        public static readonly TimeSpan HardBanPause = TimeSpan.FromMinutes(15);

        /// <summary>Pause for a hard ban that arrives again before a successful request. Ring is still refusing us, so back off longer.</summary>
        public static readonly TimeSpan RepeatHardBanPause = TimeSpan.FromMinutes(30);

        /// <summary>Result of <see cref="RecordHardBan"/>: the pause applied, and whether the caller should raise the hard-ban alert.</summary>
        public readonly record struct HardBanOutcome(bool AlertRaised, TimeSpan Pause);

        /// <summary>Minimum time between the starts of consecutive turns.</summary>
        public TimeSpan MinInterval
        {
            get { lock (_state) { return _minInterval; } }
            set { lock (_state) { _minInterval = value; } }
        }

        /// <summary>Total extra spacing added by <see cref="RecordRateLimited"/>. Survives <see cref="MinInterval"/> being reset.</summary>
        public TimeSpan RateLimitPenalty { get; private set; }

        /// <summary>True from the first 429 or hard ban of an incident until <see cref="MarkRequestSucceeded"/>. Tests read it.</summary>
        public bool IsInRateLimitIncident
        {
            get { lock (_state) { return _inRateLimitIncident; } }
        }

        /// <summary>Creates a pacer. Defaults: the system clock, a real delay, and a one-second interval.</summary>
        public MediaRequestPacer(Func<DateTime>? utcNow = null, Func<TimeSpan, CancellationToken, Task>? delay = null, TimeSpan? minInterval = null)
        {
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
            _delay = delay ?? ((wait, ct) => Task.Delay(wait, ct));
            _minInterval = minInterval ?? TimeSpan.FromSeconds(1);
        }

        /// <summary>
        /// Records a 429 from the provider. The pause always extends (never shortens) to Retry-After (or 60 seconds when absent).
        /// Spacing grows by one second only when this call starts a new incident, so retries that hit 429 again inside the same
        /// incident add nothing. Returns true when this call started a new incident, so callers can alert once per incident.
        /// </summary>
        public bool RecordRateLimited(TimeSpan? retryAfter)
        {
            lock (_state)
            {
                DateTime pauseUntil = _utcNow() + (retryAfter ?? DefaultRateLimitPause);
                // A later, shorter Retry-After must not shorten a pause already in force.
                if (!_pauseUntilUtc.HasValue || pauseUntil > _pauseUntilUtc.Value)
                {
                    _pauseUntilUtc = pauseUntil;
                }

                if (_inRateLimitIncident)
                {
                    return false;
                }

                _inRateLimitIncident = true;
                _minInterval += TimeSpan.FromSeconds(1);
                RateLimitPenalty += TimeSpan.FromSeconds(1);
                return true;
            }
        }

        /// <summary>
        /// Records a hard ban from the provider. Pauses for <see cref="HardBanPause"/>, or <see cref="RepeatHardBanPause"/> when
        /// another hard ban arrived since the last success, or Retry-After when that is longer. Never shortens an existing pause.
        /// Adds no spacing penalty. Starts the rate-limit incident if none is active, so a 429 before the next success is part of
        /// the same incident (no extra spacing, no new alert). Alerts once per ban window: returns AlertRaised false while the
        /// window from the last alert is still active.
        /// </summary>
        public HardBanOutcome RecordHardBan(TimeSpan? retryAfter)
        {
            lock (_state)
            {
                DateTime now = _utcNow();
                // Why the repeat rule keys on "since last success" rather than "window active": Ring keeps refusing until a
                // request actually succeeds, so a ban that lapses and recurs without a success is still a repeat.
                TimeSpan rule = _hardBanSinceSuccess ? RepeatHardBanPause : HardBanPause;
                TimeSpan pause = retryAfter.HasValue && retryAfter.Value > rule ? retryAfter.Value : rule;
                DateTime pauseUntil = now + pause;
                if (!_pauseUntilUtc.HasValue || pauseUntil > _pauseUntilUtc.Value)
                {
                    _pauseUntilUtc = pauseUntil;
                }

                bool windowActive = _hardBanWindowUntilUtc.HasValue && now < _hardBanWindowUntilUtc.Value;
                _hardBanSinceSuccess = true;
                _inRateLimitIncident = true;
                _hardBanWindowUntilUtc = windowActive && _hardBanWindowUntilUtc > pauseUntil ? _hardBanWindowUntilUtc : pauseUntil;
                return new HardBanOutcome(AlertRaised: !windowActive, Pause: pause);
            }
        }

        /// <summary>
        /// Ends the current rate-limit incident after a successful media request. The next 429 starts a new incident.
        /// Also ends the hard-ban escalation, so the next hard ban uses <see cref="HardBanPause"/> again.
        /// Does not reduce <see cref="MinInterval"/>.
        /// </summary>
        public void MarkRequestSucceeded()
        {
            lock (_state)
            {
                _inRateLimitIncident = false;
                _hardBanSinceSuccess = false;
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
