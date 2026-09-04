using System.Collections.Concurrent;
using System.Security.Cryptography;
using VideoForensics.Data.Common.Entities;

namespace VideoForensics.Hosting
{
    /// <summary>A pending pairing invitation, valid until first use or expiry.</summary>
    public record PairingTokenInfo(string Token, OperatorRole Role, DateTime ExpiresAtUtc);

    /// <summary>
    /// Short-lived (~2 min), single-use pairing tokens (plan §5.1). Deliberately in-memory, not
    /// database-backed - the whole point is a short window, and a server restart invalidating any
    /// pending pairing in flight is an acceptable, desirable behavior (nothing pairing-sensitive
    /// should survive a restart silently).
    /// </summary>
    public interface IPairingTokenService
    {
        PairingTokenInfo CreateToken(OperatorRole role);
        PairingTokenInfo? Peek(string token);

        /// <summary>Validates and removes the token in one step - a token can only ever be consumed once.</summary>
        bool TryConsume(string token, out OperatorRole role);
    }

    public class PairingTokenService : IPairingTokenService
    {
        private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(2);
        private readonly ConcurrentDictionary<string, PairingTokenInfo> _tokens = new();

        public PairingTokenInfo CreateToken(OperatorRole role)
        {
            PruneExpired();

            var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24))
                .Replace('+', '-').Replace('/', '_').TrimEnd('=');
            var info = new PairingTokenInfo(token, role, DateTime.UtcNow + TokenLifetime);
            _tokens[token] = info;
            return info;
        }

        public PairingTokenInfo? Peek(string token)
        {
            if (_tokens.TryGetValue(token, out var info) && info.ExpiresAtUtc > DateTime.UtcNow)
            {
                return info;
            }

            return null;
        }

        public bool TryConsume(string token, out OperatorRole role)
        {
            if (_tokens.TryRemove(token, out var info) && info.ExpiresAtUtc > DateTime.UtcNow)
            {
                role = info.Role;
                return true;
            }

            role = default;
            return false;
        }

        private void PruneExpired()
        {
            var now = DateTime.UtcNow;
            foreach (var kvp in _tokens)
            {
                if (kvp.Value.ExpiresAtUtc <= now)
                {
                    _tokens.TryRemove(kvp.Key, out _);
                }
            }
        }
    }
}
