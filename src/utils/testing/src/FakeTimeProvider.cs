namespace VideoForensics.Utils.Testing
{
    /// <summary>
    /// A clock the test controls. Starts at <see cref="DateTimeOffset.UtcNow"/> unless a fixed time is given,
    /// and moves only when <see cref="SetUtcNow"/> is called, so time-based code is deterministic.
    /// </summary>
    public sealed class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow;

        /// <summary>Starts at the current UTC time.</summary>
        public FakeTimeProvider()
            : this(DateTime.UtcNow)
        {
        }

        /// <summary>Starts at <paramref name="fixedTime"/>, read as UTC.</summary>
        public FakeTimeProvider(DateTime fixedTime)
        {
            _utcNow = new DateTimeOffset(fixedTime, TimeSpan.Zero);
        }

        /// <summary>Moves the clock to <paramref name="utcNow"/>, read as UTC.</summary>
        public void SetUtcNow(DateTime utcNow)
        {
            _utcNow = new DateTimeOffset(utcNow, TimeSpan.Zero);
        }

        /// <inheritdoc />
        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
