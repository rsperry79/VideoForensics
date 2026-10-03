using Microsoft.Extensions.Caching.Memory;

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

    /// <summary>
    /// In-memory implementation of IWebAuthnCeremonyCache using IMemoryCache with TTL-based expiration.
    /// Uses single-use semantics: TryTake removes the entry on retrieval.
    /// </summary>
    public class WebAuthnCeremonyCache : IWebAuthnCeremonyCache
    {
        private static readonly TimeSpan CeremonyLifetime = TimeSpan.FromMinutes(5);
        private readonly IMemoryCache _cache;

        public WebAuthnCeremonyCache(IMemoryCache cache)
        {
            _cache = cache;
        }

        public string Store(string optionsJson)
        {
            string nonce = Guid.NewGuid().ToString("N");
            _cache.Set(nonce, optionsJson, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CeremonyLifetime
            });
            return nonce;
        }

        public string? TryTake(string nonce)
        {
            if (_cache.TryGetValue(nonce, out string? optionsJson))
            {
                // Remove the entry (single-use semantics)
                _cache.Remove(nonce);
                return optionsJson;
            }

            return null;
        }
    }
}
