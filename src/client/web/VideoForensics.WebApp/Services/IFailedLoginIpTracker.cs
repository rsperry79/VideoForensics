using VideoForensics.Data.Common.Entities;

namespace VideoForensics.WebApp.Services
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
    }
}
