using VideoForensics.Data.Common.Entities;
using VideoForensics.Client.Hosting;

namespace VideoForensics.Client.WebApp.Api
{
    /// <summary>
    /// Limits a passkey-setup session (issued by POST /api/v1/auth/login/password/create-passkey). Such a session
    /// is accepted only on the passkey setup endpoints, for at most 15 minutes, and ends as soon as any passkey is
    /// created after it was issued. Once it ends, the operator signs in normally and hits two-factor as usual.
    /// </summary>
    internal static class PasskeySetupSessionPolicy
    {
        /// <summary>The only request paths a passkey-setup session may call. Exact match, so a longer path never qualifies.</summary>
        private static readonly string[] AllowedPaths =
        {
            "/api/v1/auth/operator-credentials/register/options",
            "/api/v1/auth/operator-credentials/register/complete",
            "/api/devices-management/operator-credentials/mine",
            "/api/v1/auth/session-kind"
        };

        public static bool IsPathAllowed(string path)
        {
            return AllowedPaths.Any(allowed => string.Equals(path, allowed, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>True once the session's 15-minute lifetime has passed. A missing issued-at (default) is never treated as expired here; the token's own expiry still applies.</summary>
        public static bool IsLifetimeEnded(DateTime issuedAtUtc, DateTime utcNow)
        {
            return issuedAtUtc != default && utcNow > issuedAtUtc + SessionTokenService.PasskeySetupSessionLifetime;
        }

        /// <summary>
        /// True when any passkey (approved, pending or revoked) was created after the session was issued. Such a
        /// passkey is the operator completing setup, so the limited session has done its job. A missing issued-at
        /// (default) skips the check, as for tokens minted before the claim existed.
        /// </summary>
        public static bool IsSupersededByPasskeyCreation(IReadOnlyList<OperatorCredential> credentials, DateTime issuedAtUtc)
        {
            return issuedAtUtc != default && credentials.Any(c => c.CreatedAtUtc > issuedAtUtc);
        }
    }
}
