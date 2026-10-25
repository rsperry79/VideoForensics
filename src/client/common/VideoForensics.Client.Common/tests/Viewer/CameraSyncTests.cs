using VideoForensics.Client.Common.Viewer;

using Xunit;

namespace VideoForensics.Client.Common.Tests.Viewer
{
    /// <summary>
    /// Tests for <see cref="CameraSync"/>: keeping multi-camera tiles aligned with the master clock.
    /// </summary>
    public class CameraSyncTests
    {
        /// <summary>Verifies that a tile within tolerance of the master clock is left alone.</summary>
        [Fact]
        public void NeedsResync_WithinTolerance_ReturnsFalse()
        {
            Assert.False(CameraSync.NeedsResync(tileSeconds: 10.3, masterSeconds: 10.0, toleranceSeconds: 0.5));
        }

        /// <summary>Verifies that a tile drifting past tolerance is resynced, whichever direction it drifted.</summary>
        [Theory]
        [InlineData(10.8, 10.0)]
        [InlineData(9.2, 10.0)]
        public void NeedsResync_BeyondTolerance_ReturnsTrue(double tile, double master)
        {
            Assert.True(CameraSync.NeedsResync(tile, master, toleranceSeconds: 0.5));
        }

        /// <summary>Verifies that drift is signed: positive when the tile is ahead of the master clock.</summary>
        [Fact]
        public void DriftSeconds_TileAhead_IsPositive()
        {
            Assert.Equal(0.75, CameraSync.DriftSeconds(tileSeconds: 10.75, masterSeconds: 10.0), precision: 6);
        }

        /// <summary>Verifies that a negative tolerance is rejected, since it would resync every tile on every tick.</summary>
        [Fact]
        public void NeedsResync_NegativeTolerance_Throws()
        {
            _ = Assert.Throws<ArgumentOutOfRangeException>(() => CameraSync.NeedsResync(1, 1, toleranceSeconds: -0.1));
        }

        /// <summary>Verifies the tile count limit: at least one tile, and no more than the grid maximum.</summary>
        [Theory]
        [InlineData(0, false)]
        [InlineData(1, true)]
        [InlineData(CameraSync.MaxTiles, true)]
        [InlineData(CameraSync.MaxTiles + 1, false)]
        public void IsTileCountAllowed_Bounds(int count, bool expected)
        {
            Assert.Equal(expected, CameraSync.IsTileCountAllowed(count));
        }
    }
}
