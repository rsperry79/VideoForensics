using System.Collections.Concurrent;

namespace VideoForensics.Hosting
{
    /// <summary>
    /// Short-lived server-side cache bridging a WebAuthn ceremony's two HTTP round-trips (options ->
    /// complete): Fido2NetLib's MakeNewCredentialAsync/MakeAssertionAsync need the exact
    /// CredentialCreateOptions/AssertionOptions object handed back that was returned from the
    /// preceding RequestNewCredential/GetAssertionOptions call, so it must be held server-side
    /// between the two requests rather than trusted from the client. In-memory only, matching
    /// IPairingTokenService's reasoning: short TTL, a server restart invalidating an in-flight
    /// ceremony is fine.
    /// </summary>
    public interface IWebAuthnCeremonyCache
    {
        string Store(string optionsJson);
        string? TryTake(string nonce);
    }

    public class WebAuthnCeremonyCache : IWebAuthnCeremonyCache
    {
        private static readonly TimeSpan CeremonyLifetime = TimeSpan.FromMinutes(5);
        private readonly ConcurrentDictionary<string, (string OptionsJson, DateTime ExpiresAtUtc)> _entries = new();

        public string Store(string optionsJson)
        {
            var nonce = Guid.NewGuid().ToString("N");
            _entries[nonce] = (optionsJson, DateTime.UtcNow + CeremonyLifetime);
            return nonce;
        }

        public string? TryTake(string nonce)
        {
            if (_entries.TryRemove(nonce, out var entry) && entry.ExpiresAtUtc > DateTime.UtcNow)
            {
                return entry.OptionsJson;
            }

            return null;
        }
    }
}
