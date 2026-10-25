namespace VideoForensics.Client.Common.Viewer
{
    /// <summary>A clip on the review timeline: its id, when it starts (UTC), how long it runs, and an optional thumbnail.</summary>
    /// <param name="ThumbnailUrl">Image shown behind the clip's timeline segment. Null or empty draws a plain segment.</param>
    public sealed record ClipWindow(string Id, DateTime StartUtc, double DurationSeconds, string? ThumbnailUrl = null);

    /// <summary>The clip that covers a time, and how far into that clip the time falls.</summary>
    public sealed record ClipSelection(string ClipId, double OffsetSeconds);

    /// <summary>
    /// Maps times to positions on the review timeline strip, and finds the clip that plays at a given time.
    /// Pure functions, so the strip, the player, and the tests agree on the same rules.
    /// </summary>
    public static class ReviewTimeline
    {
        /// <summary>
        /// Returns the horizontal pixel for <paramref name="timeUtc"/> across a window. Times outside the window clamp to the edges,
        /// so a clip that starts before the window still draws at the left edge.
        /// </summary>
        /// <exception cref="ArgumentException">The window is empty or reversed.</exception>
        public static double ToPixel(DateTime timeUtc, DateTime windowStartUtc, DateTime windowEndUtc, double widthPx)
        {
            double fraction = FractionOf(timeUtc, windowStartUtc, windowEndUtc);
            return fraction * widthPx;
        }

        /// <summary>Returns the time at a pixel across a window. The inverse of <see cref="ToPixel"/>, used when the user clicks the strip.</summary>
        /// <exception cref="ArgumentException">The window is empty or reversed.</exception>
        public static DateTime FromPixel(double xPx, DateTime windowStartUtc, DateTime windowEndUtc, double widthPx)
        {
            ValidateWindow(windowStartUtc, windowEndUtc);
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(widthPx);

            double fraction = Math.Clamp(xPx / widthPx, 0, 1);
            return windowStartUtc.AddSeconds(fraction * (windowEndUtc - windowStartUtc).TotalSeconds);
        }

        /// <summary>
        /// Returns the first clip that covers <paramref name="timeUtc"/>, with the offset into it. A clip includes its start and
        /// excludes its end, so at a boundary the later clip wins. Returns null in a gap, so the player can show a gap rather than stale video.
        /// </summary>
        public static ClipSelection? ClipAt(IReadOnlyList<ClipWindow> clips, DateTime timeUtc)
        {
            foreach (ClipWindow clip in clips)
            {
                double offset = (timeUtc - clip.StartUtc).TotalSeconds;
                if (offset >= 0 && offset < clip.DurationSeconds)
                {
                    return new ClipSelection(clip.Id, offset);
                }
            }

            return null;
        }

        private static double FractionOf(DateTime timeUtc, DateTime windowStartUtc, DateTime windowEndUtc)
        {
            ValidateWindow(windowStartUtc, windowEndUtc);
            double fraction = (timeUtc - windowStartUtc).TotalSeconds / (windowEndUtc - windowStartUtc).TotalSeconds;
            return Math.Clamp(fraction, 0, 1);
        }

        private static void ValidateWindow(DateTime windowStartUtc, DateTime windowEndUtc)
        {
            if (windowEndUtc <= windowStartUtc)
            {
                throw new ArgumentException("The review window must end after it starts.");
            }
        }
    }
}
