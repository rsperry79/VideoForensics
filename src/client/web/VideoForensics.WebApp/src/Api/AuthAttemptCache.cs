using Microsoft.Extensions.Caching.Memory;

namespace VideoForensics.WebApp.Api
{
    /// <summary>
    /// Contract for a per-process in-memory cache that stores authentication attempt state
    /// during the two-factor authentication flow. Attempts are tied to an AuthAttemptId
    /// and automatically expire after a short window (5 minutes).
    /// </summary>
    public interface IAuthAttemptCache
    {
        /// <summary>
        /// Stores provider name (nullable), username and password for a future two-factor authentication call.
        /// Returns a new AuthAttemptId that ties the 2FA request back to this attempt.
        /// </summary>
        Guid StoreAttempt(string? providerName, string username, string password);

        /// <summary>
        /// Retrieves and removes stored credentials for a given attempt ID.
        /// Returns null if the attempt doesn't exist or has expired.
        /// </summary>
        (string? ProviderName, string? Username, string? Password)? GetAndRemoveAttempt(Guid attemptId);
    }

    /// <summary>
    /// Composite object for storing auth attempt data in cache.
    /// </summary>
    internal class AuthAttemptData
    {
        public string? ProviderName { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    /// <summary>
    /// In-memory implementation of IAuthAttemptCache using IMemoryCache with automatic expiry.
    /// IMemoryCache is thread-safe, so no manual locking is needed.
    /// </summary>
    public class AuthAttemptCache : IAuthAttemptCache
    {
        private readonly IMemoryCache _cache;
        private readonly TimeSpan _ttl = TimeSpan.FromMinutes(5);

        public AuthAttemptCache(IMemoryCache cache)
        {
            _cache = cache;
        }

        public Guid StoreAttempt(string? providerName, string username, string password)
        {
            var attemptId = Guid.NewGuid();
            var data = new AuthAttemptData
            {
                ProviderName = providerName,
                Username = username,
                Password = password
            };

            _cache.Set(attemptId, data, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = _ttl
            });

            return attemptId;
        }

        public (string? ProviderName, string? Username, string? Password)? GetAndRemoveAttempt(Guid attemptId)
        {
            if (_cache.TryGetValue(attemptId, out AuthAttemptData? data))
            {
                // Remove the entry (single-use semantics)
                _cache.Remove(attemptId);
                return (data?.ProviderName, data?.Username, data?.Password);
            }

            return null;
        }
    }
}
