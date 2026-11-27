namespace VideoForensics.Core.Providers.Common.Services
{
    /// <summary>
    /// Classifies a snapshot frame as a placeholder. A dead camera serves a placeholder image that is byte-identical on every
    /// request, while a live JPEG carries sensor noise and never repeats its hash. Offline is checked first, so an offline
    /// device reports <see cref="SnapshotPlaceholderReason.Offline"/> even when its frame also matches the previous one.
    /// </summary>
    public static class SnapshotPlaceholderDetector
    {
        /// <summary>
        /// Returns the placeholder reason for a frame, or null when the frame looks real.
        /// </summary>
        /// <param name="connected">Device connection state, or null when unknown.</param>
        /// <param name="sha256Hex">SHA-256 of the current frame, as hex. Must not be null or whitespace.</param>
        /// <param name="previousSha256Hex">SHA-256 of the previous frame, as hex, or null when there is none.</param>
        /// <exception cref="ArgumentException"><paramref name="sha256Hex"/> is null, empty or whitespace.</exception>
        public static SnapshotPlaceholderReason? Check(bool? connected, string sha256Hex, string? previousSha256Hex)
        {
            if (string.IsNullOrWhiteSpace(sha256Hex))
            {
                throw new ArgumentException("A frame hash is required.", nameof(sha256Hex));
            }

            if (connected == false)
            {
                return SnapshotPlaceholderReason.Offline;
            }

            if (!string.IsNullOrEmpty(previousSha256Hex)
                && string.Equals(sha256Hex, previousSha256Hex, StringComparison.OrdinalIgnoreCase))
            {
                return SnapshotPlaceholderReason.IdenticalFrame;
            }

            return null;
        }
    }
}
