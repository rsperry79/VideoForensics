using Microsoft.AspNetCore.DataProtection;

using System.Text.Json;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Hosting
{
    /// <summary>Which kind of credential produced this session, and therefore how PairedDeviceAuthenticationHandler must re-validate it on every request.</summary>
    public enum CredentialKind
    {
        /// <summary>A PairedDevice row (service/device pairing — MAUI, MCP bridge, other headless clients). Unchanged behavior from before this change.</summary>
        ServiceDevice,
        /// <summary>A plain username+password login — no specific credential row, just the Operator itself.</summary>
        Password,
        /// <summary>A username-first passkey bound to an OperatorCredential row.</summary>
        OperatorPasskey
    }

    /// <summary>Claims carried by an issued session token.</summary>
    public record SessionPrincipal(Guid OperatorId, Guid? CredentialId, CredentialKind Kind, OperatorRole Role, Guid SecurityStampAtIssuance, DateTime IssuedAtUtc, DateTime ExpiresAtUtc);

    /// <summary>
    /// Issues and validates opaque bearer session tokens for paired devices, using ASP.NET Core
    /// Data Protection (already registered by AddVideoForensicsDatabase) rather than a separate JWT
    /// library - the token is just protected+serialized SessionPrincipal JSON, so no extra signing
    /// key management is needed beyond what Data Protection already provides.
    ///
    /// IMPORTANT: this only validates the token's own content (signature + expiry) - it does NOT by
    /// itself confirm the paired device is still active, nor that the operator's password has not been
    /// reset since token issuance. A revoked device's already-issued token would still decrypt/validate
    /// successfully here; the caller (PairedDeviceAuthenticationHandler) MUST separately check
    /// IPairedDeviceRepository for current revocation status on every request and compare the token's
    /// SecurityStampAtIssuance against the current Operator.SecurityStamp to detect password resets,
    /// so a revocation or password reset takes effect within one request round-trip rather than only
    /// at token expiry (plan §5.4's "locked out within one request round-trip, not eventually" requirement).
    /// </summary>
    public interface ISessionTokenService
    {
        string Issue(Guid operatorId, Guid? credentialId, CredentialKind kind, OperatorRole role, Guid securityStamp);
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

        public string Issue(Guid operatorId, Guid? credentialId, CredentialKind kind, OperatorRole role, Guid securityStamp)
        {
            DateTime now = DateTime.UtcNow;
            var principal = new SessionPrincipal(operatorId, credentialId, kind, role, securityStamp, now, now + SessionLifetime);
            string json = JsonSerializer.Serialize(principal);
            return _protector.Protect(json);
        }

        public SessionPrincipal? Validate(string token)
        {
            try
            {
                string json = _protector.Unprotect(token);
                SessionPrincipal? principal = JsonSerializer.Deserialize<SessionPrincipal>(json);
                return principal == null || principal.ExpiresAtUtc < DateTime.UtcNow ? null : principal;
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
