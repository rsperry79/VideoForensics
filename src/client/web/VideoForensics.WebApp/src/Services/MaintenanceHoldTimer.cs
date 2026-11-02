namespace VideoForensics.Client.WebApp.Services
{
    /// <summary>Result of asking the timer to extend the active hold.</summary>
    public enum HoldRenewResult
    {
        /// <summary>The token is the active hold and its safety timer was restarted.</summary>
        Renewed,

        /// <summary>The token is not the active hold.</summary>
        NotActive,

        /// <summary>The hold reached its maximum age and was not extended. The safety timer still ends it at that point.</summary>
        MaxAgeReached,
    }

    /// <summary>
    /// Owns the one active offline-tool maintenance hold and ends it when nobody does. Registered as a singleton: the
    /// coordinator is scoped, so the begin and end requests of one hold never share a coordinator instance, and the hold
    /// slot must outlive them both.
    /// </summary>
    public sealed class MaintenanceHoldTimer
    {
        /// <summary>How long a hold lasts without a renew or end call. Long enough for a large database repair, short enough to recover unattended.</summary>
        public static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(30);

        /// <summary>Longest a hold can last from when it begins, however often it is renewed. Stops a renewing tool from holding the server forever.</summary>
        public static readonly TimeSpan DefaultMaxAge = TimeSpan.FromHours(4);

        private readonly object _lock = new();
        private readonly TimeSpan _timeout;
        private readonly TimeSpan _maxAge;
        private readonly TimeProvider _clock;
        private Timer? _timer;
        private Guid? _active;
        private Action? _onExpired;
        private DateTimeOffset? _startedAt;
        private long _generation;

        /// <param name="timeout">Time after the hold starts or is renewed before it is ended.</param>
        public MaintenanceHoldTimer(TimeSpan timeout)
            : this(timeout, DefaultMaxAge, TimeProvider.System)
        {
        }

        /// <param name="timeout">Time after the hold starts or is renewed before it is ended.</param>
        /// <param name="maxAge">Longest the hold can last counted from its start. Renewals never extend past it.</param>
        /// <param name="clock">Time source for the hold start and maximum age. Tests supply a fake clock.</param>
        public MaintenanceHoldTimer(TimeSpan timeout, TimeSpan maxAge, TimeProvider clock)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxAge, TimeSpan.Zero);
            ArgumentNullException.ThrowIfNull(clock);
            _timeout = timeout;
            _maxAge = maxAge;
            _clock = clock;
        }

        /// <summary>When the active hold began, or null when no hold is active.</summary>
        public DateTimeOffset? HoldStartedAt
        {
            get
            {
                lock (_lock)
                {
                    return _startedAt;
                }
            }
        }

        /// <summary>
        /// Takes the active-hold slot without starting the safety timer. Returns the hold token, or null when another hold is
        /// already active. The caller arms the timer with <see cref="ArmSafetyTimer"/> once the hold is in effect.
        /// </summary>
        public Guid? TryReserve(Action onExpired)
        {
            ArgumentNullException.ThrowIfNull(onExpired);
            lock (_lock)
            {
                if (_active is not null)
                {
                    return null;
                }

                var token = Guid.NewGuid();
                _active = token;
                _onExpired = onExpired;
                // Why: the maximum age counts from the begin call, so time spent draining counts against it.
                _startedAt = _clock.GetUtcNow();
                return token;
            }
        }

        /// <summary>
        /// Takes the active-hold slot and starts the safety timer. Returns the hold token, or null when another hold is
        /// already active. <paramref name="onExpired"/> runs once, only if the hold is still active when the timer fires.
        /// </summary>
        public Guid? TryStart(Action onExpired)
        {
            Guid? token = TryReserve(onExpired);
            if (token is not null)
            {
                _ = ArmSafetyTimer(token.Value);
            }

            return token;
        }

        /// <summary>
        /// Starts the safety timer for the reserved hold. Returns false when <paramref name="token"/> is not the active hold.
        /// Called once the hold is in effect, so the timer never counts down during a drain.
        /// </summary>
        public bool ArmSafetyTimer(Guid token)
        {
            lock (_lock)
            {
                if (_active != token)
                {
                    return false;
                }

                Arm(token);
                return true;
            }
        }

        /// <summary>Restarts the safety timer for the active hold. Returns false when the hold is not active or has reached its maximum age.</summary>
        public bool Renew(Guid token) => RenewOutcome(token) == HoldRenewResult.Renewed;

        /// <summary>
        /// Restarts the safety timer for the active hold and reports why it could not, if it could not. The timer never runs
        /// past the hold's maximum age, so a renew at or after that age is refused and the hold is not extended.
        /// </summary>
        public HoldRenewResult RenewOutcome(Guid token)
        {
            lock (_lock)
            {
                if (_active != token)
                {
                    return HoldRenewResult.NotActive;
                }

                if (RemainingAgeLocked() <= TimeSpan.Zero)
                {
                    return HoldRenewResult.MaxAgeReached;
                }

                // Why: the timer keeps its callback, so a renew only restarts the clock. The hold stays owned by the same token.
                Arm(token);
                return HoldRenewResult.Renewed;
            }
        }

        /// <summary>
        /// Ends the hold when <paramref name="token"/> is the active one: stops the timer, frees the slot and runs
        /// <paramref name="release"/> while still holding the lock, so a new hold cannot start before the gate is released.
        /// Returns false, and runs nothing, when the token is not the active hold.
        /// </summary>
        public bool TryEnd(Guid token, Action release)
        {
            ArgumentNullException.ThrowIfNull(release);
            lock (_lock)
            {
                if (_active != token)
                {
                    return false;
                }

                ClearLocked();
                release();
                return true;
            }
        }

        // Why: the timer waits the renew window, but never past the maximum age, so the cap ends the hold even if nobody renews.
        private TimeSpan RemainingAgeLocked() => _startedAt!.Value + _maxAge - _clock.GetUtcNow();

        // Why: each arm gets a new generation, so a callback queued by an earlier arm does nothing when it fires.
        private void Arm(Guid token)
        {
            _generation++;
            long generation = _generation;
            _timer?.Dispose();
            TimeSpan wait = _timeout;
            TimeSpan remainingAge = RemainingAgeLocked();
            if (remainingAge < wait)
            {
                wait = remainingAge;
            }

            if (wait < TimeSpan.Zero)
            {
                wait = TimeSpan.Zero;
            }

            _timer = new Timer(_ => Expire(token, generation), null, wait, Timeout.InfiniteTimeSpan);
        }

        private void ClearLocked()
        {
            _generation++;
            _timer?.Dispose();
            _timer = null;
            _active = null;
            _onExpired = null;
            _startedAt = null;
        }

        private void Expire(Guid token, long generation)
        {
            lock (_lock)
            {
                if (generation != _generation || _onExpired is null)
                {
                    return;
                }

                // Why: the expiry goes through the same token-checked end path as an explicit end, so it can only end its own hold.
                _ = TryEnd(token, _onExpired);
            }
        }
    }
}
