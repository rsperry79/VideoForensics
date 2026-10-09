using System;

namespace VideoForensics.Providers.Ring
{
    /// <summary>
    /// Persists the Ring hard-ban expiry so it survives process restarts. Implementations must not
    /// throw: persistence is best-effort and failures are treated as "no persisted state".
    /// </summary>
    public interface IHardBanStateStore
    {
        /// <summary>Reads the persisted hard-ban expiry (UTC), or null when nothing valid is persisted.</summary>
        DateTime? Read();

        /// <summary>Persists the hard-ban expiry (UTC).</summary>
        /// <param name="untilUtc">When the hard ban expires.</param>
        void Write(DateTime untilUtc);

        /// <summary>Removes any persisted hard-ban expiry.</summary>
        void Clear();
    }
}
