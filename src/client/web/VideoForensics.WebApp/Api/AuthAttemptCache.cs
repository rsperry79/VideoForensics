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
        /// Stores username and password for a future two-factor authentication call.
        /// Returns a new AuthAttemptId that ties the 2FA request back to this attempt.
        /// </summary>
        Guid StoreAttempt(string username, string password);

        /// <summary>
        /// Retrieves and removes stored credentials for a given attempt ID.
        /// Returns null if the attempt doesn't exist or has expired.
        /// </summary>
        (string? Username, string? Password)? GetAndRemoveAttempt(Guid attemptId);
    }

    /// <summary>
    /// In-memory, singleton implementation of IAuthAttemptCache with automatic expiry.
    /// </summary>
    public class AuthAttemptCache : IAuthAttemptCache
    {
        private readonly object _lock = new();
        private readonly Dictionary<Guid, (string Username, string Password, DateTime ExpiresAt)> _attempts = new();
        private readonly TimeSpan _ttl = TimeSpan.FromMinutes(5);

        public Guid StoreAttempt(string username, string password)
        {
            lock (_lock)
            {
                // Clean expired entries on each store
                CleanExpiredAttempts();

                var attemptId = Guid.NewGuid();
                _attempts[attemptId] = (username, password, DateTime.UtcNow.Add(_ttl));
                return attemptId;
            }
        }

        public (string? Username, string? Password)? GetAndRemoveAttempt(Guid attemptId)
        {
            lock (_lock)
            {
                if (_attempts.TryGetValue(attemptId, out var attempt))
                {
                    // Check if expired
                    if (attempt.ExpiresAt <= DateTime.UtcNow)
                    {
                        _attempts.Remove(attemptId);
                        return null;
                    }

                    // Return and remove (consume the attempt)
                    _attempts.Remove(attemptId);
                    return (attempt.Username, attempt.Password);
                }

                return null;
            }
        }

        private void CleanExpiredAttempts()
        {
            var now = DateTime.UtcNow;
            var expiredIds = _attempts
                .Where(kvp => kvp.Value.ExpiresAt <= now)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var id in expiredIds)
            {
                _attempts.Remove(id);
            }
        }
    }
}
