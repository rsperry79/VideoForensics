namespace VideoForensics.Core.Providers.Core
{
    /// <summary>Output size rules for timeline MP4s.</summary>
    public static class TimelineSize
    {
        /// <summary>
        /// Caps the height at <paramref name="maxHeight"/>, keeping the aspect ratio. Both dimensions round up to even numbers,
        /// which the H.264 encoder requires. A size already within the cap is returned unchanged.
        /// </summary>
        public static (int Width, int Height) Cap(int width, int height, int maxHeight)
        {
            if (height <= maxHeight)
            {
                return (width, height);
            }

            int cappedWidth = (int)Math.Round(width * (double)maxHeight / height);
            return (RoundUpToEven(cappedWidth), RoundUpToEven(maxHeight));
        }

        private static int RoundUpToEven(int value)
        {
            return value % 2 == 0 ? value : value + 1;
        }
    }
}
