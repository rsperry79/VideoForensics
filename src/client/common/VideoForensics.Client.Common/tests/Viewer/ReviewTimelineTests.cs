using VideoForensics.Client.Common.Viewer;

using Xunit;

namespace VideoForensics.Client.Common.Tests.Viewer
{
    /// <summary>
    /// Tests for <see cref="ReviewTimeline"/>: time-to-pixel mapping and clip lookup for the review timeline.
    /// </summary>
    public class ReviewTimelineTests
    {
        private static readonly DateTime Start = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime End = Start.AddMinutes(10);

        /// <summary>Verifies that the window start maps to pixel zero and the window end to the full width.</summary>
        [Fact]
        public void ToPixel_WindowEdges_MapToZeroAndWidth()
        {
            Assert.Equal(0, ReviewTimeline.ToPixel(Start, Start, End, 600));
            Assert.Equal(600, ReviewTimeline.ToPixel(End, Start, End, 600));
        }

        /// <summary>Verifies that a time halfway through the window lands halfway across the strip.</summary>
        [Fact]
        public void ToPixel_Midpoint_MapsToHalfWidth()
        {
            Assert.Equal(300, ReviewTimeline.ToPixel(Start.AddMinutes(5), Start, End, 600), precision: 6);
        }

        /// <summary>Verifies that times outside the window clamp to the strip edges instead of drawing off-screen.</summary>
        [Fact]
        public void ToPixel_OutsideWindow_ClampsToEdges()
        {
            Assert.Equal(0, ReviewTimeline.ToPixel(Start.AddMinutes(-3), Start, End, 600));
            Assert.Equal(600, ReviewTimeline.ToPixel(End.AddMinutes(3), Start, End, 600));
        }

        /// <summary>Verifies that pixel-to-time is the inverse of time-to-pixel, so a click seeks to the time it shows.</summary>
        [Fact]
        public void FromPixel_RoundTripsWithToPixel()
        {
            DateTime time = Start.AddSeconds(137);

            double x = ReviewTimeline.ToPixel(time, Start, End, 800);
            DateTime back = ReviewTimeline.FromPixel(x, Start, End, 800);

            Assert.True(Math.Abs((back - time).TotalMilliseconds) < 1, $"round trip drifted by {(back - time).TotalMilliseconds} ms");
        }

        /// <summary>Verifies that an empty or reversed window is rejected rather than dividing by zero.</summary>
        [Fact]
        public void ToPixel_EmptyWindow_Throws()
        {
            _ = Assert.Throws<ArgumentException>(() => ReviewTimeline.ToPixel(Start, Start, Start, 600));
        }

        /// <summary>Verifies that the clip covering a time is found, with the offset into that clip.</summary>
        [Fact]
        public void ClipAt_TimeInsideClip_ReturnsClipAndOffset()
        {
            var clips = new List<ClipWindow>
            {
                new("a", Start, 60),
                new("b", Start.AddSeconds(60), 60),
            };

            ClipSelection? selection = ReviewTimeline.ClipAt(clips, Start.AddSeconds(75));

            Assert.NotNull(selection);
            Assert.Equal("b", selection.ClipId);
            Assert.Equal(15, selection.OffsetSeconds, precision: 6);
        }

        /// <summary>Verifies that a time in a gap between clips finds no clip, so the player can show a gap instead of stale video.</summary>
        [Fact]
        public void ClipAt_TimeInGap_ReturnsNull()
        {
            var clips = new List<ClipWindow>
            {
                new("a", Start, 30),
                new("b", Start.AddSeconds(60), 30),
            };

            Assert.Null(ReviewTimeline.ClipAt(clips, Start.AddSeconds(45)));
        }

        /// <summary>Verifies that a clip's end is exclusive, so the next clip owns the boundary second.</summary>
        [Fact]
        public void ClipAt_BoundaryBetweenAdjacentClips_BelongsToNextClip()
        {
            var clips = new List<ClipWindow>
            {
                new("a", Start, 60),
                new("b", Start.AddSeconds(60), 60),
            };

            Assert.Equal("b", ReviewTimeline.ClipAt(clips, Start.AddSeconds(60))!.ClipId);
        }
    }
}
