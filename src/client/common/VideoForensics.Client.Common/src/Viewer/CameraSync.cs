namespace VideoForensics.Client.Common.Viewer
{
    /// <summary>
    /// Keeps multi-camera tiles aligned with one master clock. Each tile is resynced only when it drifts past a tolerance,
    /// so the player does not seek on every tick.
    /// </summary>
    public static class CameraSync
    {
        /// <summary>The most tiles the grid shows at once (a 3 by 3 grid).</summary>
        public const int MaxTiles = 9;

        /// <summary>Default drift, in seconds, a tile may have before it is resynced.</summary>
        public const double DefaultToleranceSeconds = 0.5;

        /// <summary>Signed drift: positive when the tile is ahead of the master clock.</summary>
        public static double DriftSeconds(double tileSeconds, double masterSeconds)
        {
            return tileSeconds - masterSeconds;
        }

        /// <summary>True when the tile has drifted farther than <paramref name="toleranceSeconds"/> from the master clock, in either direction.</summary>
        /// <exception cref="ArgumentOutOfRangeException">The tolerance is negative.</exception>
        public static bool NeedsResync(double tileSeconds, double masterSeconds, double toleranceSeconds)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(toleranceSeconds);
            return Math.Abs(DriftSeconds(tileSeconds, masterSeconds)) > toleranceSeconds;
        }

        /// <summary>True when the tile count is between one and <see cref="MaxTiles"/>.</summary>
        public static bool IsTileCountAllowed(int count)
        {
            return count >= 1 && count <= MaxTiles;
        }
    }
}
