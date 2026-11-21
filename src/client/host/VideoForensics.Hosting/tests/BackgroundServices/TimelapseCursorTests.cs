using VideoForensics.Client.Hosting.BackgroundServices;

using Xunit;

namespace VideoForensics.Client.Hosting.Tests.BackgroundServices
{
    /// <summary>
    /// The per-device cursor is the latest of: the end of its latest time lapse, its latest event at or before the horizon, and
    /// its earliest capture. A skipped short gap writes no row, so the closing event keeps the cursor moving past it.
    /// </summary>
    public class TimelapseCursorTests
    {
        private static readonly DateTime T = new(2026, 11, 6, 10, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void Resolve_NothingKnown_ReturnsNull()
        {
            Assert.Null(TimelapseCursor.Resolve(null, null, null));
        }

        [Fact]
        public void Resolve_OnlyEarliestCapture_ReturnsIt()
        {
            Assert.Equal(T, TimelapseCursor.Resolve(null, null, T));
        }

        [Fact]
        public void Resolve_SkippedGapAfterLatestEvent_CursorMovesPastTheEvent()
        {
            // A short gap closed by an event at 10:43 wrote no time lapse, so the latest time lapse ends earlier.
            DateTime? cursor = TimelapseCursor.Resolve(T.AddMinutes(-60), T.AddMinutes(43), T);

            Assert.Equal(T.AddMinutes(43), cursor);
        }

        [Fact]
        public void Resolve_LatestTimelapseEndIsNewest_ReturnsIt()
        {
            DateTime? cursor = TimelapseCursor.Resolve(T.AddMinutes(50), T.AddMinutes(20), T);

            Assert.Equal(T.AddMinutes(50), cursor);
        }
    }
}
