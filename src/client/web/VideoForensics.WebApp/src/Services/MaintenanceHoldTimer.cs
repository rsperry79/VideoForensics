namespace VideoForensics.Client.WebApp.Services
{
    /// <summary>
    /// Owns the one active offline-tool maintenance hold and ends it when nobody does. Registered as a singleton: the
    /// coordinator is scoped, so the begin and end requests of one hold never share a coordinator instance, and the hold
    /// slot must outlive them both.
    /// </summary>
    public sealed class MaintenanceHoldTimer
    {
        /// <summary>How long a hold lasts without a renew or end call. Long enough for a large database repair, short enough to recover unattended.</summary>
        public static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(30);

        private readonly object _lock = new();
        private readonly TimeSpan _timeout;
        private Timer? _timer;
        private Guid? _active;
        private Action? _onExpired;
        private long _generation;

        /// <param name="timeout">Time after the hold starts or is renewed before it is ended.</param>
        public MaintenanceHoldTimer(TimeSpan timeout)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
            _timeout = timeout;
        }

        /// <summary>
        /// Takes the active-hold slot and starts the safety timer. Returns the hold token, or null when another hold is
        /// already active. <paramref name="onExpired"/> runs once, only if the hold is still active when the timer fires.
        /// </summary>
        public Guid? TryStart(Action onExpired)
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
                Arm(token);
                return token;
            }
        }

        /// <summary>Restarts the safety timer for the active hold. Returns false when <paramref name="token"/> is not the active hold.</summary>
        public bool Renew(Guid token)
        {
            lock (_lock)
            {
                if (_active != token)
                {
                    return false;
                }

                // Why: the timer keeps its callback, so a renew only restarts the clock. The hold stays owned by the same token.
                Arm(token);
                return true;
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

        // Why: each arm gets a new generation, so a callback queued by an earlier arm does nothing when it fires.
        private void Arm(Guid token)
        {
            _generation++;
            long generation = _generation;
            _timer?.Dispose();
            _timer = new Timer(_ => Expire(token, generation), null, _timeout, Timeout.InfiniteTimeSpan);
        }

        private void ClearLocked()
        {
            _generation++;
            _timer?.Dispose();
            _timer = null;
            _active = null;
            _onExpired = null;
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
