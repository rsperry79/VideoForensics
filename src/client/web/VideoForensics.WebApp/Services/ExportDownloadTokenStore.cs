using System.Collections.Concurrent;

namespace VideoForensics.WebApp.Services
{
    /// <summary>
    /// Maps a short-lived, single-use opaque token to a server-side export archive file path, so the
    /// browser-download endpoint never needs to accept a raw filesystem path from the client. Entries
    /// expire after a few minutes and are removed once downloaded (or found expired).
    /// </summary>
    public sealed class ExportDownloadTokenStore
    {
        private static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(10);
        private readonly ConcurrentDictionary<Guid, (string FilePath, DateTime ExpiresAtUtc)> _tokens = new();

        public Guid CreateToken(string filePath)
        {
            var token = Guid.NewGuid();
            _tokens[token] = (filePath, DateTime.UtcNow.Add(TokenLifetime));
            return token;
        }

        /// <summary>Consumes (removes) the token if present and not expired, returning its file path.</summary>
        public string? TryConsume(Guid token)
        {
            if (!_tokens.TryRemove(token, out var entry))
            {
                return null;
            }

            return entry.ExpiresAtUtc >= DateTime.UtcNow ? entry.FilePath : null;
        }
    }
}
