namespace VideoForensics.Client.Hosting.BackgroundServices
{
    /// <summary>An account whose time-lapse check has stalled, with the provider name and interval for the alert.</summary>
    public sealed record TimelapseCheckStall(string ProviderName, TimeSpan CheckInterval);

    /// <summary>
    /// Tracks each account's time-lapse check. An account stalls when its check has not completed for twice its interval,
    /// counted from its last completion or, before any, from its first attempt. Each stall is reported once, and again only
    /// after a completion followed by another stall. Pure logic, so the scheduler reads the clock and logs the result.
    /// </summary>
    public sealed class TimelapseCheckWatchdog
    {
        private sealed record Tracked(string ProviderName, TimeSpan Interval, DateTime LastProgressUtc, bool Reported);

        private readonly Dictionary<Guid, Tracked> _accounts = [];

        /// <summary>Records that a check started for the account. The first attempt starts its clock.</summary>
        public void Attempted(Guid accountId, string providerName, TimeSpan interval, DateTime nowUtc)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
            if (_accounts.TryGetValue(accountId, out Tracked? existing))
            {
                _accounts[accountId] = existing with { ProviderName = providerName, Interval = interval };
            }
            else
            {
                _accounts[accountId] = new Tracked(providerName, interval, nowUtc, Reported: false);
            }
        }

        /// <summary>Records that a check completed without failures. It resets the clock and clears any earlier report.</summary>
        public void Completed(Guid accountId, DateTime nowUtc)
        {
            if (_accounts.TryGetValue(accountId, out Tracked? existing))
            {
                _accounts[accountId] = existing with { LastProgressUtc = nowUtc, Reported = false };
            }
        }

        /// <summary>
        /// The accounts that have newly stalled at <paramref name="nowUtc"/>. Each is returned once; it is not returned again
        /// until it completes and stalls again.
        /// </summary>
        public IReadOnlyList<TimelapseCheckStall> TakeNewStalls(DateTime nowUtc)
        {
            var stalls = new List<TimelapseCheckStall>();
            foreach (Guid accountId in _accounts.Keys.ToList())
            {
                Tracked tracked = _accounts[accountId];
                bool stalled = nowUtc - tracked.LastProgressUtc > tracked.Interval * 2;
                if (stalled && !tracked.Reported)
                {
                    stalls.Add(new TimelapseCheckStall(tracked.ProviderName, tracked.Interval));
                    _accounts[accountId] = tracked with { Reported = true };
                }
            }

            return stalls;
        }
    }
}
