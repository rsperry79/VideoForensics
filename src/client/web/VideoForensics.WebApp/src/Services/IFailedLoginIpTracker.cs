using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Client.WebApp.Services
{
    /// <summary>
    /// Fail2ban-style tracker: counts distinct operator accounts locked out from one client IP
    /// within a sliding window, and auto-bans that IP when the threshold is crossed.
    /// </summary>
    public interface IFailedLoginIpTracker
    {
        /// <summary>
        /// Records that <paramref name="operatorId"/> was just locked out from <paramref name="clientIp"/>.
        /// Returns true only when this call created a temporary BannedIpRange for the IP.
        /// Does nothing (returns false) when <paramref name="policy"/> has auto-ban disabled.
        /// </summary>
        /// <param name="clientIp">Resolved client IP (from INetworkTierResolver.ResolveClientIp), never a raw forwarded header.</param>
        /// <param name="operatorId">The account that just locked out. Counted once per window regardless of repeats.</param>
        /// <param name="policy">Current lockout policy, supplying the threshold, window and ban duration.</param>
        /// <param name="ct">Cancellation token.</param>
        Task<bool> RecordLockoutAsync(string clientIp, Guid operatorId, LockoutPolicySettings policy, CancellationToken ct);

        /// <summary>
        /// Forgets the in-memory lockout counter and any auto-ban suppression for <paramref name="clientIp"/>.
        /// Called after an admin removes a single-host ban, so the address is not re-banned on its next
        /// lockout because of counts accumulated before the unban. A no-op for an unknown address.
        /// </summary>
        /// <param name="clientIp">Single host address (IPv4 or IPv6), as written in the ban's CIDR.</param>
        /// <param name="ct">Cancellation token.</param>
        Task ClearAsync(string clientIp, CancellationToken ct);
    }
}
