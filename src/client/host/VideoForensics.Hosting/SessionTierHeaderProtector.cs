using Microsoft.AspNetCore.DataProtection;

using System.Text.Json;

using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Hosting
{
    /// <summary>Header name(s) used to carry a Blazor Server circuit's real network tier across a self-HTTP call back into this same process (see <see cref="ISessionTierHeaderProtector"/>).</summary>
    public static class SessionTierHeaderNames
    {
        public const string HeaderName = "X-VF-Session-Tier";
    }

    /// <summary>
    /// Protects/unprotects the short-lived {tier, operatorId, expiry} payload the WebApp's own
    /// Blazor Server UI attaches to its self-HTTP calls (see AddSelfHttpService) via the
    /// <c>X-VF-Session-Tier</c> header, so PairedDeviceAuthenticationHandler can recover the
    /// CALLING CIRCUIT'S real network tier - resolved once, from the browser's actual connection,
    /// when the circuit started - instead of resolving the self-call's own connection (which is
    /// always loopback, since the WebApp is calling itself, regardless of where the real browser
    /// physically is).
    ///
    /// operatorId is bound into the payload so a header captured for one operator's session cannot
    /// be replayed to elevate a DIFFERENT operator's request; the short (&lt;= 2 minute) expiry
    /// bounds how long a captured header could be replayed at all. Data Protection's own
    /// integrity/authenticity guarantee means a tampered or forged header fails to unprotect
    /// entirely, rather than silently decoding to attacker-chosen values.
    /// </summary>
    public interface ISessionTierHeaderProtector
    {
        /// <summary>Produces the header value for this operator's session, valid for a short, fixed lifetime.</summary>
        string Protect(NetworkTier tier, Guid operatorId);

        /// <summary>True if the header value unprotects to an unexpired payload; the recovered tier/operatorId are set only when this returns true.</summary>
        bool TryUnprotect(string headerValue, out NetworkTier tier, out Guid operatorId);
    }

    public class SessionTierHeaderProtector : ISessionTierHeaderProtector
    {
        private static readonly TimeSpan MaxLifetime = TimeSpan.FromMinutes(2);
        private readonly IDataProtector _protector;
        private readonly TimeProvider _timeProvider;

        private record SessionTierHeaderPayload(NetworkTier Tier, Guid OperatorId, DateTime ExpiresAtUtc);

        public SessionTierHeaderProtector(IDataProtectionProvider provider, TimeProvider? timeProvider = null)
        {
            _protector = provider.CreateProtector("VideoForensics.SessionTierHeader.v1");
            _timeProvider = timeProvider ?? TimeProvider.System;
        }

        public string Protect(NetworkTier tier, Guid operatorId)
        {
            DateTime expiresAtUtc = _timeProvider.GetUtcNow().UtcDateTime + MaxLifetime;
            var payload = new SessionTierHeaderPayload(tier, operatorId, expiresAtUtc);
            return _protector.Protect(JsonSerializer.Serialize(payload));
        }

        public bool TryUnprotect(string headerValue, out NetworkTier tier, out Guid operatorId)
        {
            tier = default;
            operatorId = default;

            if (string.IsNullOrEmpty(headerValue))
            {
                return false;
            }

            try
            {
                SessionTierHeaderPayload? payload = JsonSerializer.Deserialize<SessionTierHeaderPayload>(_protector.Unprotect(headerValue));
                if (payload == null || payload.ExpiresAtUtc <= _timeProvider.GetUtcNow().UtcDateTime)
                {
                    return false;
                }

                tier = payload.Tier;
                operatorId = payload.OperatorId;
                return true;
            }
            catch
            {
                // Malformed, tampered, or protected under a since-rotated key - treat as absent
                // rather than throwing; the caller falls back to the most restrictive tier.
                return false;
            }
        }
    }
}
