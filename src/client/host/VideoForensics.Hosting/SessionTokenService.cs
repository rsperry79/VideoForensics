using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Hosting
{
    /// <summary>Claims carried by an issued session token.</summary>
    public record SessionPrincipal(Guid OperatorId, Guid PairedDeviceId, OperatorRole Role, DateTime IssuedAtUtc, DateTime ExpiresAtUtc);

    /// <summary>
    /// Issues and validates opaque bearer session tokens for paired devices, using ASP.NET Core
    /// Data Protection (already registered by AddVideoForensicsDatabase) rather than a separate JWT
    /// library - the token is just protected+serialized SessionPrincipal JSON, so no extra signing
    /// key management is needed beyond what Data Protection already provides.
    ///
    /// IMPORTANT: this only validates the token's own content (signature + expiry) - it does NOT by
    /// itself confirm the paired device is still active. A revoked device's already-issued token
    /// would still decrypt/validate successfully here; the caller (PairedDeviceAuthenticationHandler)
    /// MUST separately check IPairedDeviceRepository for current revocation status on every request,
    /// so a revocation takes effect within one request round-trip rather than only at token expiry
    /// (plan §5.4's "locked out within one request round-trip, not eventually" requirement).
    /// </summary>
    public interface ISessionTokenService
    {
        string Issue(Guid operatorId, Guid pairedDeviceId, OperatorRole role);
        SessionPrincipal? Validate(string token);
    }

    public class SessionTokenService : ISessionTokenService
    {
        private static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(12);
        private readonly IDataProtector _protector;

        public SessionTokenService(IDataProtectionProvider provider)
        {
            _protector = provider.CreateProtector("VideoForensics.SessionTokens.v1");
        }

        public string Issue(Guid operatorId, Guid pairedDeviceId, OperatorRole role)
        {
            var now = DateTime.UtcNow;
            var principal = new SessionPrincipal(operatorId, pairedDeviceId, role, now, now + SessionLifetime);
            var json = JsonSerializer.Serialize(principal);
            return _protector.Protect(json);
        }

        public SessionPrincipal? Validate(string token)
        {
            try
            {
                var json = _protector.Unprotect(token);
                var principal = JsonSerializer.Deserialize<SessionPrincipal>(json);
                if (principal == null || principal.ExpiresAtUtc < DateTime.UtcNow)
                {
                    return null;
                }

                return principal;
            }
            catch
            {
                // Malformed, tampered, or protected under a since-rotated key - treat as invalid,
                // never throw for what is routinely just "bad/expired token" on an auth path.
                return null;
            }
        }
    }
}
