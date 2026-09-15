using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace VideoForensics.Hosting
{
    /// <summary>Status of a device-code pairing session.</summary>
    public enum DeviceCodePairingStatus
    {
        Pending,
        Approved,
        Expired
    }

    /// <summary>
    /// A pending device-code pairing session (RFC 8628-style), for a headless client (e.g. the MCP
    /// stdio bridge) that has no browser of its own. The CLI polls using DeviceCode (long, high-entropy,
    /// never shown to a human); a human enters UserCode (short, typeable) into the server's own UI to
    /// approve it. Once approved, ApiKey holds the raw fallback API key EXACTLY ONCE - the first poll
    /// that reads it clears it, mirroring PairingTokenService's single-use TryConsume pattern.
    /// </summary>
    public class DeviceCodePairingSession
    {
        public required string DeviceCode { get; init; }
        public required string UserCode { get; init; }
        public DeviceCodePairingStatus Status { get; set; } = DeviceCodePairingStatus.Pending;
        public DateTime ExpiresAtUtc { get; init; }
        public string? ApiKey { get; set; }
    }

    /// <summary>
    /// Short-lived (~10 min), in-memory device-code pairing sessions for headless clients that can't
    /// perform a browser-based WebAuthn ceremony (see PairingTokenService for the browser-based
    /// equivalent). Deliberately not database-backed for the same reason PairingTokenService isn't: a
    /// server restart invalidating any pending pairing in flight is acceptable/desirable.
    /// </summary>
    public interface IDeviceCodePairingService
    {
        /// <summary>Creates a new pending session with fresh DeviceCode/UserCode.</summary>
        DeviceCodePairingSession CreateSession();

        /// <summary>Looks up a session by its human-typeable UserCode (used by the approval endpoint). Null if not found/expired.</summary>
        DeviceCodePairingSession? GetByUserCode(string userCode);

        /// <summary>Looks up a session by its DeviceCode (used by the CLI's poll endpoint). Null if not found/expired.</summary>
        DeviceCodePairingSession? GetByDeviceCode(string deviceCode);

        /// <summary>Marks the session (found by UserCode) Approved and attaches the raw API key. Returns false if not found/expired/already approved.</summary>
        bool TryApprove(string userCode, string apiKey);

        /// <summary>
        /// Reads and clears the API key for a DeviceCode's session in one atomic step - the key can only
        /// ever be retrieved once. Returns null if the session doesn't exist, isn't Approved yet, or the
        /// key was already retrieved by an earlier poll.
        /// </summary>
        string? TryConsumeApiKey(string deviceCode);
    }

    public class DeviceCodePairingService : IDeviceCodePairingService
    {
        private static readonly TimeSpan SessionLifetime = TimeSpan.FromMinutes(10);
        private static readonly string UnambiguousAlphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789"; // Exclude 0/O/1/I/L and lowercase

        private readonly ConcurrentDictionary<string, DeviceCodePairingSession> _sessionsByDeviceCode = new();
        private readonly ConcurrentDictionary<string, DeviceCodePairingSession> _sessionsByUserCode = new();

        public DeviceCodePairingSession CreateSession()
        {
            PruneExpired();

            // DeviceCode: long, high-entropy, never shown to humans
            string deviceCode = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .Replace('+', '-').Replace('/', '_').TrimEnd('=');

            // UserCode: 8-character code from unambiguous alphabet, human-typeable
            string userCode = GenerateUserCode(8);

            var session = new DeviceCodePairingSession
            {
                DeviceCode = deviceCode,
                UserCode = userCode,
                ExpiresAtUtc = DateTime.UtcNow + SessionLifetime
            };

            _sessionsByDeviceCode[deviceCode] = session;
            _sessionsByUserCode[userCode] = session;

            return session;
        }

        public DeviceCodePairingSession? GetByUserCode(string userCode)
        {
            return _sessionsByUserCode.TryGetValue(userCode, out DeviceCodePairingSession? session) && session.ExpiresAtUtc > DateTime.UtcNow ? session : null;
        }

        public DeviceCodePairingSession? GetByDeviceCode(string deviceCode)
        {
            return _sessionsByDeviceCode.TryGetValue(deviceCode, out DeviceCodePairingSession? session) && session.ExpiresAtUtc > DateTime.UtcNow ? session : null;
        }

        public bool TryApprove(string userCode, string apiKey)
        {
            DeviceCodePairingSession? session = GetByUserCode(userCode);
            if (session == null || session.Status != DeviceCodePairingStatus.Pending)
            {
                return false;
            }

            session.ApiKey = apiKey;
            session.Status = DeviceCodePairingStatus.Approved;
            return true;
        }

        public string? TryConsumeApiKey(string deviceCode)
        {
            DeviceCodePairingSession? session = GetByDeviceCode(deviceCode);
            if (session == null || session.Status != DeviceCodePairingStatus.Approved || session.ApiKey == null)
            {
                return null;
            }

            string apiKey = session.ApiKey;
            session.ApiKey = null;
            return apiKey;
        }

        private void PruneExpired()
        {
            DateTime now = DateTime.UtcNow;
            var expiredDeviceCodes = new List<string>();
            var expiredUserCodes = new List<string>();

            foreach (KeyValuePair<string, DeviceCodePairingSession> kvp in _sessionsByDeviceCode)
            {
                if (kvp.Value.ExpiresAtUtc <= now)
                {
                    expiredDeviceCodes.Add(kvp.Key);
                }
            }

            foreach (KeyValuePair<string, DeviceCodePairingSession> kvp in _sessionsByUserCode)
            {
                if (kvp.Value.ExpiresAtUtc <= now)
                {
                    expiredUserCodes.Add(kvp.Key);
                }
            }

            foreach (string code in expiredDeviceCodes)
            {
                _ = _sessionsByDeviceCode.TryRemove(code, out _);
            }

            foreach (string code in expiredUserCodes)
            {
                _ = _sessionsByUserCode.TryRemove(code, out _);
            }
        }

        private static string GenerateUserCode(int length)
        {
            char[] code = new char[length];
            using (var rng = RandomNumberGenerator.Create())
            {
                byte[] buffer = new byte[length];
                rng.GetBytes(buffer);
                for (int i = 0; i < length; i++)
                {
                    code[i] = UnambiguousAlphabet[buffer[i] % UnambiguousAlphabet.Length];
                }
            }

            return new string(code);
        }
    }
}
