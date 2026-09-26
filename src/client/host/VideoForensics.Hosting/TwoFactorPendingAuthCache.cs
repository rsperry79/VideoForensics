using System.Collections.Concurrent;

namespace VideoForensics.Hosting
{
    /// <summary>
    /// Short-lived server-side cache for correlating password-login step-1 with WebAuthn-passkey step-2
    /// in the two-factor authentication flow: when an operator successfully enters their password but
    /// has 2FA required, they receive an opaque correlation token. They then present that token when
    /// calling the standalone WebAuthn assertion endpoint to complete step-2, establishing they are
    /// the same person who passed step-1. In-memory only with a 5-minute TTL (same as WebAuthnCeremonyCache),
    /// matching the reasoning: a server restart invalidating an in-flight 2FA flow is fine.
    /// </summary>
    public interface ITwoFactorPendingAuthCache
    {
        /// <summary>
        /// Stores a pending 2FA step-1 (password login) for a specific operator, returns an opaque
        /// correlation token that the client presents to complete step-2 (WebAuthn assertion).
        /// </summary>
        string Store(Guid operatorId);

        /// <summary>
        /// Retrieves the operator ID for a pending 2FA flow by its correlation token, or null if
        /// the token is invalid, expired, or already consumed (single-use).
        /// </summary>
        Guid? TryTake(string correlationToken);
    }

    /// <summary>
    /// In-memory implementation of ITwoFactorPendingAuthCache with TTL-based expiration.
    /// </summary>
    public class TwoFactorPendingAuthCache : ITwoFactorPendingAuthCache
    {
        private static readonly TimeSpan PendingAuthLifetime = TimeSpan.FromMinutes(5);
        private readonly ConcurrentDictionary<string, (Guid OperatorId, DateTime ExpiresAtUtc)> _entries = new();

        public string Store(Guid operatorId)
        {
            string correlationToken = Guid.NewGuid().ToString("N");
            _entries[correlationToken] = (operatorId, DateTime.UtcNow + PendingAuthLifetime);
            return correlationToken;
        }

        public Guid? TryTake(string correlationToken)
        {
            if (_entries.TryRemove(correlationToken, out (Guid OperatorId, DateTime ExpiresAtUtc) entry))
            {
                if (entry.ExpiresAtUtc > DateTime.UtcNow)
                {
                    return entry.OperatorId;
                }
            }

            return null;
        }
    }
}
